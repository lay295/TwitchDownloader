using System.Collections.Generic;
using System.Xml.Serialization;

namespace TwitchDownloader.Models
{
	[XmlRoot(ElementName = "ResourceDictionary", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
	public class ResourceDictionaryModel
	{
		[XmlElement(ElementName = "SolidColorBrush", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
		public List<SolidColorBrushModel> SolidColorBrush { get; set; }

		[XmlElement(ElementName = "Boolean", Namespace = "clr-namespace:System;assembly=mscorlib")]
		public List<BooleanModel> Boolean { get; set; }

		[XmlAttribute(AttributeName = "xmlns")]
		public string Xmlns { get; set; }

		[XmlAttribute(AttributeName = "x", Namespace = "http://www.w3.org/2000/xmlns/")]
		public string X { get; set; }

		[XmlAttribute(AttributeName ="system", Namespace = "clr-namespace:System;assembly=mscorlib")]
		public string System { get; set; }
	}
}
