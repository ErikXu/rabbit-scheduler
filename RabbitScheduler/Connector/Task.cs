using System;
using System.Collections.Generic;

namespace Connector
{
    public class Task : Entity
    {
        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 间隔（单位分钟）
        /// </summary>
        public int Interval { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        public TaskStatus Status { get; set; }

        /// <summary>
        /// 子任务
        /// </summary>
        public List<SubTask> SubTasks { get; set; }
    }

    public enum TaskStatus
    {
        /// <summary>
        /// 正常
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 完成
        /// </summary>
        Finished = 1,

        /// <summary>
        /// 撤销
        /// </summary>
        Canceled = 2
    }

    public class SubTask : Entity
    {
        public DateTime SendTime { get; set; }

        public bool IsSent { get; set; }
    }
}