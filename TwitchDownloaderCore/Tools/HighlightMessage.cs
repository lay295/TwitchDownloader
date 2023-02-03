using SkiaSharp;
using System;
using System.IO;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    public enum HighlightType
    {
        None,
        SubscribedTier,
        SubscribedPrime,
        GiftedMany,
        GiftedSingle,
        ContinuingGift,
        GiftedAnonymous
    }

    public class HighlightMessage : IDisposable
    {
        public bool Disposed = false;

        private const string SUBSCRIBED_TIER_ICON = "m 32.599229,13.144498 c 1.307494,-2.80819 5.494049,-2.80819 6.80154,0 l 5.648628,12.140919 13.52579,1.877494 c 3.00144,0.418654 4.244522,3.893468 2.138363,5.967405 -3.357829,3.309501 -6.715662,6.618992 -10.073491,9.928491 L 53.07148,56.81637 c 0.524928,2.962772 -2.821092,5.162303 -5.545572,3.645496 L 36,54.043603 24.474093,60.461866 C 21.749613,61.975455 18.403591,59.779142 18.92852,56.81637 L 21.359942,43.058807 11.286449,33.130316 c -2.1061588,-2.073937 -0.863074,-5.548751 2.138363,-5.967405 l 13.52579,-1.877494 z";
        private const string SUBSCRIBED_PRIME_ICON = "m 61.894653,21.663055 v 25.89488 c 0,3.575336 -2.898361,6.47372 -6.473664,6.47372 H 16.57901 c -3.573827,-0.0036 -6.470094,-2.89986 -6.473663,-6.47372 V 21.663055 L 23.052674,31.373635 36,18.426194 c 4.315772,4.315816 8.631553,8.631629 12.947323,12.947441 z";
        private const string GIFTED_SINGLE_ICON = "m 55.187956,23.24523 h 6.395987 V 42.433089 H 58.38595 V 61.620947 H 13.614042 V 42.433089 H 10.416049 V 23.24523 h 6.395987 v -3.859957 c 0,-8.017328 9.689919,-12.0307888 15.359963,-6.363975 0.418936,0.418935 0.796298,0.879444 1.125692,1.371934 l 2.702305,4.055034 2.702305,-4.055034 a 8.9863623,8.9863139 0 0 1 1.125692,-1.371934 c 5.666845,-5.6668138 15.359963,-1.653353 15.359963,6.363975 z M 23.208023,19.385273 v 3.859957 h 8.301992 l -3.536982,-5.305444 a 2.6031666,2.6031528 0 0 0 -4.76501,1.445487 z m 25.583946,0 v 3.859957 h -8.301991 l 3.536983,-5.305444 a 2.6031666,2.6031528 0 0 1 4.765008,1.442286 z m 6.395987,10.255909 v 6.395951 H 39.19799 v -6.395951 z m -3.197992,25.58381 V 42.433089 H 39.19799 V 55.224992 Z M 32.802003,29.641182 v 6.395951 H 16.812036 v -6.395951 z m 0,12.791907 H 20.010028 v 12.791903 h 12.791975 z";
        private const string GIFTED_ANONYMOUS_ICON = "m 54.571425,64.514958 a 4.3531428,4.2396967 0 0 1 -1.273998,-0.86096 l -1.203426,-1.172067 a 7.0051428,6.822584 0 0 0 -9.90229,0 c -3.417139,3.328092 -8.962569,3.328092 -12.383427,0 l -0.159707,-0.155553 a 7.1871427,6.9998405 0 0 0 -9.854005,-0.28216 l -1.894286,1.635103 a 4.9362858,4.8076423 0 0 1 -3.276,1.215474 H 10 V 32.337399 a 26.000001,25.322423 0 0 1 52,0 v 32.557396 h -5.627146 c -0.627714,0 -1.240569,-0.133847 -1.801429,-0.379837 z M 35.999996,14.249955 A 18.571428,18.087444 0 0 0 17.428572,32.337399 v 22.515245 a 14.619428,14.238435 0 0 1 17.471998,2.358609 l 0.163448,0.155554 c 0.516285,0.50645 1.355715,0.50645 1.875712,0 a 14.437428,14.061179 0 0 1 17.631712,-2.11623 V 32.337399 A 18.571428,18.087444 0 0 0 35.999996,14.249955 Z M 24.857142,35.954887 a 3.7142855,3.6174889 0 1 1 7.42857,0 3.7142855,3.6174889 0 0 1 -7.42857,0 z m 18.571432,-3.617488 a 3.7142859,3.6174892 0 1 0 0,7.234978 3.7142859,3.6174892 0 0 0 0,-7.234978 z";

        private SKBitmap _subscribedTierIcon = null;
        private SKBitmap _subscribedPrimeIcon = null;
        private SKBitmap _giftSingleIcon = null;
        private SKBitmap _giftManyIcon = null;
        private SKBitmap _giftAnonymousIcon = null;

        private static readonly Regex _subMessageRegex = new(@"^(subscribed (?:with Prime|at Tier \d)\. They've subscribed for \d?\d?\d months(?:, currently on a \d?\d?\d month streak)?! )(.+)$", RegexOptions.Compiled);
        private static readonly Regex _giftAnonymousRegex = new(@"^An anonymous user (?:gifted a|is gifting \d\d?\d?) Tier \d", RegexOptions.Compiled);

        // If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck
        public static bool IsSubMessage(Comment comment)
        {
            if (IsHighlightedMessage(comment) != HighlightType.None)
            {
                return true;
            }
            return false;
        }

        public static HighlightType IsHighlightedMessage(Comment comment)
        {
            if (comment.message.body.StartsWith(comment.commenter.display_name + " subscribed at Tier"))
            {
                return HighlightType.SubscribedTier;
            }
            if (comment.message.body.StartsWith(comment.commenter.display_name + " subscribed with Prime"))
            {
                return HighlightType.SubscribedPrime;
            }
            if (comment.message.body.StartsWith(comment.commenter.display_name + " is gifting"))
            {
                return HighlightType.GiftedMany;
            }
            if (comment.message.body.StartsWith(comment.commenter.display_name + " gifted a Tier"))
            {
                return HighlightType.GiftedSingle;
            }
            if (comment.message.body.StartsWith(comment.commenter.display_name + " is continuing the Gift Sub"))
            {
                return HighlightType.ContinuingGift;
            }
            if (comment.commenter._id is "274598607" && _giftAnonymousRegex.IsMatch(comment.message.body)) // '274598607' is the id of the anonymous gift message account, display name 'AnAnonymousGifter'
            {
                return HighlightType.GiftedAnonymous;
            }
            // There is one more resub along the lines of "...is on month # of # of their Tier # Gift Sub from..." but I don't know the exact wording and it's very rare
            return HighlightType.None;
        }

        /// <returns>A copy of the requested icon or null if no icon exists for the highlight type</returns>
        public SKBitmap GetHighlightIcon(HighlightType highlightType, string purple, SKColor textColor, double fontSize)
        {
            // Return a copy of the needed icon from cache or generate if null
            return highlightType switch
            {
                HighlightType.SubscribedTier => _subscribedTierIcon?.Copy() ?? GenerateHighlightIcon(highlightType, purple, textColor, fontSize),
                HighlightType.SubscribedPrime => _subscribedPrimeIcon?.Copy() ?? GenerateHighlightIcon(highlightType, purple, textColor, fontSize),
                HighlightType.GiftedSingle => _giftSingleIcon?.Copy() ?? GenerateHighlightIcon(highlightType, purple, textColor, fontSize),
                HighlightType.GiftedMany => _giftManyIcon?.Copy() ?? GenerateHighlightIcon(highlightType, purple, textColor, fontSize),
                HighlightType.GiftedAnonymous => _giftAnonymousIcon?.Copy() ?? GenerateHighlightIcon(highlightType, purple, textColor, fontSize),
                _ => null
            };
        }

        private SKBitmap GenerateHighlightIcon(HighlightType highlightType, string purple, SKColor textColor, double fontSize)
        {
            SKBitmap returnBitmap;

            // Generate the needed icon
            if (highlightType is HighlightType.GiftedMany)
            {
                using MemoryStream ms = new(Properties.Resources.gift_illustration); // Illustration is 72x72
                SKCodec codec = SKCodec.Create(ms);
                using SKBitmap tempBitmap = SKBitmap.Decode(codec);
                //int newSize = (int)(fontSize / 0.2727); // 44*44px @ 12pt font // Doesn't work because our image sections aren't tall enough and I'm not rewriting that right now
                int newSize = (int)(fontSize / 0.6); // 20x20px @ 12pt font
                SKImageInfo imageInfo = new(newSize, newSize);
                returnBitmap = tempBitmap.Resize(imageInfo, SKFilterQuality.High);
            }
            else
            {
                using SKBitmap tempBitmap = new(72, 72); // Icon SVG strings are scaled for 72x72
                using SKCanvas tempCanvas = new(tempBitmap);

                SKPath iconPath = SKPath.ParseSvgPathData(highlightType switch
                {
                    HighlightType.SubscribedTier => SUBSCRIBED_TIER_ICON,
                    HighlightType.SubscribedPrime => SUBSCRIBED_PRIME_ICON,
                    HighlightType.GiftedSingle => GIFTED_SINGLE_ICON,
                    HighlightType.GiftedAnonymous => GIFTED_ANONYMOUS_ICON,
                    _ => throw new NotImplementedException("This should not be possible.")
                });
                iconPath.FillType = SKPathFillType.EvenOdd;

                SKPaint iconColor = new()
                {
                    Color = highlightType switch
                    {
                        HighlightType.SubscribedTier => textColor,
                        HighlightType.SubscribedPrime => SKColor.Parse(purple),
                        HighlightType.GiftedSingle => textColor,
                        HighlightType.GiftedAnonymous => textColor,
                        _ => throw new NotImplementedException("This should not be possible.")
                    },
                    IsAntialias = true,
                    LcdRenderText = true
                };

                tempCanvas.DrawPath(iconPath, iconColor);
                int newSize = (int)(fontSize / 0.6); // 20*20px @ 12pt font
                SKImageInfo imageInfo = new(newSize, newSize);
                returnBitmap = tempBitmap.Resize(imageInfo, SKFilterQuality.High);
            }

            // Cache a copy of the icon
            switch (highlightType)
            {
                case HighlightType.SubscribedTier:
                    _subscribedTierIcon = returnBitmap.Copy();
                    break;
                case HighlightType.SubscribedPrime:
                    _subscribedPrimeIcon = returnBitmap.Copy();
                    break;
                case HighlightType.GiftedSingle:
                    _giftSingleIcon = returnBitmap.Copy();
                    break;
                case HighlightType.GiftedMany:
                    _giftManyIcon = returnBitmap.Copy();
                    break;
                case HighlightType.GiftedAnonymous:
                    _giftAnonymousIcon = returnBitmap.Copy();
                    break;
                default:
                    throw new NotImplementedException("This should not be possible.");
            }

            // Return the generated icon
            return returnBitmap;
        }

        /// <summary>
        /// Splits a comment into 2 comments based on the start index of a custom resub message
        /// </summary>
        /// <returns>
        /// 2 clones of <paramref name="comment"/> whose <see cref="Message.body"/> and <see cref="Message.fragments"/> contain the split resub details and
        /// the user's custom resub message if there is one, else the original <paramref name="comment"/> and null
        /// </returns>
        public static (Comment subMessage, Comment customMessage) SplitSubComment(Comment comment)
        {
            var (subMessage, customMessage) = SplitSubMessage(comment.message.body);
            // Return the original comment + null if there is no custom sub message
            if (customMessage is null)
            {
                return (comment, null);
            }

            // If we don't clone then both new comments reference the original commenter object, message object, fragment list, etc.
            Comment subMessageComment = comment.Clone();
            subMessageComment.message.body = subMessage;
            subMessageComment.message.fragments[0].text = subMessage;
            Comment customMessageComment = comment.Clone();
            customMessageComment.message.body = customMessage;

            // If only one fragment then we are done
            if (comment.message.fragments.Count == 1)
            {
                customMessageComment.message.fragments[0].text = customMessage;
                return (subMessageComment, customMessageComment);
            }

            // The next fragment MUST be an emote
            if (comment.message.fragments[1].emoticon is not null)
            {
                subMessageComment.message.fragments.RemoveRange(1, comment.message.fragments.Count - 1);

                // Check to see if there is a custom message before the emote
                // i.e. Foobar subscribed with Prime. They've subscribed for 45 months! Hey PogChamp
                if (!customMessage.StartsWith(comment.message.fragments[1].text)) // If yes
                {
                    customMessageComment.message.fragments[0].text = customMessage[..(customMessage.IndexOf(comment.message.fragments[1].text) - 1)];
                    return (subMessageComment, customMessageComment);
                }

                customMessageComment.message.fragments.RemoveAt(0);
                return (subMessageComment, customMessageComment);
            }

            throw new NotImplementedException("This should not be possible.");
        }

        /// <returns>The split resub details and user's custom resub message if there is one, else the resub details and null</returns>
        public static (string subMessage, string customMessage) SplitSubMessage(string commentMessage)
        {
            var subMessageMatch = _subMessageRegex.Match(commentMessage);
            if (!subMessageMatch.Success)
            {
                return (commentMessage, null);
            }

            return (subMessageMatch.Groups[1].ToString(), subMessageMatch.Groups[2].ToString());
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            try
            {
                if (Disposed)
                {
                    return;
                }

                if (isDisposing)
                {
                    _subscribedTierIcon?.Dispose();
                    _subscribedPrimeIcon?.Dispose();
                    _giftSingleIcon?.Dispose();
                    _giftManyIcon?.Dispose();
                    _giftAnonymousIcon?.Dispose();

                    // Set the root references to null to explicitly tell the garbage collector that the resources have been disposed
                    _subscribedTierIcon = null;
                    _subscribedPrimeIcon = null;
                    _giftSingleIcon = null;
                    _giftManyIcon = null;
                    _giftAnonymousIcon = null;
                }
            }
            finally
            {
                Disposed = true;
            }
        }
    }
}