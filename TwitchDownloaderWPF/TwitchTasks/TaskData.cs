using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TwitchDownloader.TwitchTasks
{
    public class TaskData
    {
        public string Id { get; set; }
        public string Streamer { get; set; }
        public string Title { get; set; }
        public ImageSource Thumbnail { get; set; }
        public DateTime Time { get; set; }
        public int Length { get; set; }
        public int Views { get; set; }
        public string LengthFormatted
        {
            get
            {
                TimeSpan time = TimeSpan.FromSeconds(Length);
                if ((int)time.TotalHours  > 0)
                {
                    return (int)time.TotalHours + ":" + time.Hours.ToString("D2") + ":" + time.Seconds.ToString("D2");
                }
                else if ((int)time.TotalMinutes > 0)
                {
                    return time.Minutes.ToString("D2") + ":" + time.Seconds.ToString("D2");
                }
                else
                {
                    return time.Seconds.ToString("D2") + "s";
                }
            }
        }
    }
}
