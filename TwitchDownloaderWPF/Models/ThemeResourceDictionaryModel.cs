using System.Collections.Generic;
using System.Xml.Serialization;

namespace TwitchDownloaderWPF.Models
{
    [XmlRoot(ElementName = "ResourceDictionary", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
    public class ThemeResourceDictionaryModel
    {
        [XmlElement(ElementName = "SolidColorBrush", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
        public List<SolidColorBrushModel> SolidColorBrush { get; set; }

        [XmlElement(ElementName = "Boolean", Namespace = "clr-namespace:System;assembly=mscorlib")]
        public List<BooleanModel> Boolean { get; set; }
    }
}