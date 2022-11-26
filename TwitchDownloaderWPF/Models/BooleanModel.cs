using System.Xml.Serialization;

namespace TwitchDownloader.Models
{
	[XmlRoot(ElementName = "Boolean", Namespace = "clr-namespace:System;assembly=mscorlib")]
	public class BooleanModel
	{
		[XmlAttribute(AttributeName = "Key", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml")]
		public string Key { get; set; }
		[XmlText(Type = typeof(bool))]
		public bool Value { get; set; }
	}
}
