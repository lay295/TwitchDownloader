﻿using System.Collections.Generic;
using System.Xml.Serialization;

namespace TwitchDownloader.Properties.Models
{
	[XmlRoot(ElementName = "SolidColorBrush", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation")]
	public class SolidColorBrushModel
	{
		[XmlAttribute(AttributeName = "Key", Namespace = "http://schemas.microsoft.com/winfx/2006/xaml")]
		public string Key { get; set; }
		[XmlAttribute(AttributeName = "Color")]
		public string Color { get; set; }
	}
}
