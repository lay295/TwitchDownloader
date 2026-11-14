// UseWPF drops System.IO and System.Net.Http from the implicit usings (to avoid clashes with WPF types); the linked Core test helpers need them.
global using System.IO;
global using System.Net.Http;
global using Xunit;
