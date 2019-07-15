using System;
using System.ComponentModel.DataAnnotations;

namespace RabbitScheduler.WebApi.Models
{
    public class TaskCreateForm
    {
        /// <summary>
        /// 任务名称
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        [Required]
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 间隔（单位分钟）
        /// </summary>
        [Required]
        public int Interval { get; set; }
    }
}