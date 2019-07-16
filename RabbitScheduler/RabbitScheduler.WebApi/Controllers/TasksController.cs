using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Connector;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitScheduler.WebApi.Models;
using Task = Connector.Task;
using TaskStatus = Connector.TaskStatus;

namespace RabbitScheduler.WebApi.Controllers
{
    [Route("api/tasks")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly IConnection _rabbitConnection;
        private readonly MongoDbContext _mongoDbContext;

        public TasksController(IConnection rabbitConnection, MongoDbContext mongoDbContext)
        {
            _rabbitConnection = rabbitConnection;
            _mongoDbContext = mongoDbContext;
        }

        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TaskCreateForm form)
        {
            var task = new Task
            {
                Name = form.Name,
                StartTime = form.StartTime,
                EndTime = form.EndTime,
                Interval = form.Interval,
                SubTasks = new List<SubTask>()
            };

            var startTime = task.StartTime;
            var endTime = task.EndTime;

            while ((endTime - startTime).TotalMinutes >= 0)
            {
                var sendTime = startTime;
                if (sendTime <= endTime && sendTime > DateTime.UtcNow)
                {
                    task.SubTasks.Add(new SubTask { Id = ObjectId.GenerateNewId(), SendTime = sendTime });
                }

                startTime = startTime.AddMinutes(task.Interval);
            }

            await _mongoDbContext.Collection<Task>().InsertOneAsync(task);

            var timeFlag = task.SubTasks[0].SendTime.ToString("yyyy-MM-dd HH:mm:ssZ");
            var exchange = "Task";
            var queue = "Task";

            var index = 0;
            var pendingExchange = "PendingTask";
            var pendingQueue = $"PendingTask|Task:{task.Id}_{index}_{timeFlag}";

            using (var channel = _rabbitConnection.CreateModel())
            {
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

                properties.Expiration = ((int)(task.SubTasks[0].SendTime - DateTime.UtcNow).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
                channel.BasicPublish(pendingExchange, pendingQueue, properties, Encoding.UTF8.GetBytes(string.Empty));
            }

            return Ok();
        }

        /// <summary>
        /// 撤销任务
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel([FromRoute]string id)
        {
            var taskId = new ObjectId(id);
            var task = await _mongoDbContext.Collection<Task>().Find(n => n.Id == taskId).SingleOrDefaultAsync();
            if (task == null)
            {
                return NotFound(new { message = "任务不存在！" });
            }

            task.Status = TaskStatus.Canceled;
            await _mongoDbContext.Collection<Task>().FindOneAndReplaceAsync(n => n.Id == taskId, task);
            return NoContent();
        }
    }
}
