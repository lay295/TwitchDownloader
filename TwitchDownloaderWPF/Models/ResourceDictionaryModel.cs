using System.Collections.Generic;
using System.Xml.Serialization;

namespace TwitchDownloader.Properties.Models
{
	[XmlRoot(ElementName = "ResourceDictionary", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
	public class ResourceDictionaryModel
	{
		[XmlElement(ElementName = "SolidColorBrush", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
		public List<SolidColorBrushModel> SolidColorBrush { get; set; }
		[XmlAttribute(AttributeName = "xmlns")]
		public string Xmlns { get; set; }
		[XmlAttribute(AttributeName = "x", Namespace = "http://www.w3.org/2000/xmlns/")]
		public string X { get; set; }
	}
}
