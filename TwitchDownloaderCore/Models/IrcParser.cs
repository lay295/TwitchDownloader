using System;
using System.Collections.Generic;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Models
{
    public class IrcParser
    {
        private readonly ITaskLogger _logger;

        public IrcParser(ITaskLogger logger)
        {
            _logger = logger;
        }

        public List<IrcMessage> Parse(ReadOnlySpan<byte> text)
        {
            var messages = new List<IrcMessage>();

            var textStart = -1;
            var textEnd = text.Length;
            var lineEnd = -1;
            var iterations = 0;
            var maxIterations = text.Count((byte)'\n') + 1;
            do
            {
                textStart++;
                iterations++;
                if (iterations > maxIterations)
                    throw new Exception("Infinite loop encountered while decoding IRC messages.");

                if (textStart >= textEnd)
                    break;

                var workingSlice = text[textStart..];
                lineEnd = workingSlice.IndexOf((byte)'\n');
                if (lineEnd != -1)
                    workingSlice = workingSlice[..lineEnd].TrimEnd((byte)'\r');

                if (workingSlice.IsWhiteSpace())
                {
                    continue;
                }

                // Parse here

                if (lineEnd == -1)
                {
                    break;
                }
            } while ((textStart += lineEnd) < textEnd);

            return messages;
        }
    }
}