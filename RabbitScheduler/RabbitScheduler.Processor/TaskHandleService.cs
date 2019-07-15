using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Connector;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitScheduler.Processor
{
    public class TaskHandleService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IConnection _rabbitConnection;
        private readonly IModel _channel;
        private readonly MongoDbContext _mongoDbContext;

        public TaskHandleService(ILogger<TaskHandleService> logger, IConnection rabbitConnection, MongoDbContext mongoDbContext)
        {
            _logger = logger;
            _mongoDbContext = mongoDbContext;

            _rabbitConnection = rabbitConnection;
            _channel = _rabbitConnection.CreateModel();
            _channel.BasicQos(0, 1, false);
        }

        public System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken)
        {
            var exchange = "Task";
            var queue = "Task";

            _channel.ExchangeDeclare(exchange, "direct", true);
            _channel.QueueDeclare(queue, true, false, false);
            _channel.QueueBind(queue, exchange, queue);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var index = (int)ea.BasicProperties.Headers["index"];
                var id = (ea.BasicProperties.Headers["id"] as byte[]).BytesToString();
                var timeFlag = (ea.BasicProperties.Headers["sendtime"] as byte[]).BytesToString();
                _channel.QueueDelete($"PendingTask|Task:{id}_{index}_{timeFlag}", false, true);

                var taskId = new ObjectId(id);
                var task = _mongoDbContext.Collection<Task>().Find(n => n.Id == taskId).SingleOrDefault();
                if (task == null || task.Status != TaskStatus.Normal)
                {
                    return;
                }

                _logger.LogInformation($"[{DateTime.UtcNow}]执行任务...");

                task.SubTasks[index].IsSent = true;

                if (task.SubTasks.Count > index + 1)
                {
                    PublishPendingMsg(_channel, task, index + 1);
                }
                else
                {
                    task.Status = TaskStatus.Finished;
                }

                _mongoDbContext.Collection<Task>().ReplaceOne(n => n.Id == taskId, task);
                _channel.BasicAck(ea.DeliveryTag, false);
            };
            _channel.BasicConsume(queue, false, consumer);
            _logger.LogInformation("任务执行服务已启动...");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void PublishPendingMsg(IModel channel, Task task, int index)
        {
            var timeFlag = task.SubTasks[index].SendTime.ToString("yyyy-MM-dd HH:mm:ssZ");

            var exchange = "Task";
            var queue = "Task";

            var pendingExchange = "PendingTask";
            var pendingQueue = $"PendingTask|Task:{task.Id}_{index}_{timeFlag}";

            channel.ExchangeDeclare(exchange, "direct", true);
            channel.QueueDeclare(queue, true, false, false);
            channel.QueueBind(queue, exchange, queue);

            var retryDic = new Dictionary<string, object>
            {
                {"x-dead-letter-exchange", exchange},
                {"x-dead-letter-routing-key", queue}
            };

            channel.ExchangeDeclare(pendingExchange, "direct", true);
            channel.QueueDeclare(pendingQueue, true, false, false, retryDic);
            channel.QueueBind(pendingQueue, pendingExchange, pendingQueue);

            var properties = channel.CreateBasicProperties();
            properties.Headers = new Dictionary<string, object>
            {
                ["index"] = index,
                ["id"] = task.Id.ToString(),
                ["sendtime"] = timeFlag
            };

            properties.Expiration = ((int)(task.SubTasks[index].SendTime - DateTime.UtcNow).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
            channel.BasicPublish(pendingExchange, pendingQueue, properties, Encoding.UTF8.GetBytes(string.Empty));
        }

        public System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("任务执行服务已停止...");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public void Dispose()
        {
            _rabbitConnection?.Dispose();
            _channel?.Dispose();
        }
    }
}