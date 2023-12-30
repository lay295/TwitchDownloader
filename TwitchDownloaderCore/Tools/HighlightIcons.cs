using SkiaSharp;
using System;
using System.IO;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.VideoPlatforms.Twitch;

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
        GiftedAnonymous,
        PayingForward,
        ChannelPointHighlight,
        Raid,
        BitBadgeTierNotification,
        WatchStreak,
        CharityDonation,
        Unknown
    }

    public sealed class HighlightIcons : IDisposable
    {
        public bool Disposed { get; private set; }

        private const string SUBSCRIBED_TIER_ICON_SVG = "m 32.599229,13.144498 c 1.307494,-2.80819 5.494049,-2.80819 6.80154,0 l 5.648628,12.140919 13.52579,1.877494 c 3.00144,0.418654 4.244522,3.893468 2.138363,5.967405 -3.357829,3.309501 -6.715662,6.618992 -10.073491,9.928491 L 53.07148,56.81637 c 0.524928,2.962772 -2.821092,5.162303 -5.545572,3.645496 L 36,54.043603 24.474093,60.461866 C 21.749613,61.975455 18.403591,59.779142 18.92852,56.81637 L 21.359942,43.058807 11.286449,33.130316 c -2.1061588,-2.073937 -0.863074,-5.548751 2.138363,-5.967405 l 13.52579,-1.877494 z";
        private const string SUBSCRIBED_PRIME_ICON_SVG = "m 61.894653,21.663055 v 25.89488 c 0,3.575336 -2.898361,6.47372 -6.473664,6.47372 H 16.57901 c -3.573827,-0.0036 -6.470094,-2.89986 -6.473663,-6.47372 V 21.663055 L 23.052674,31.373635 36,18.426194 c 4.315772,4.315816 8.631553,8.631629 12.947323,12.947441 z";
        private const string GIFTED_SINGLE_ICON_SVG = "m 55.187956,23.24523 h 6.395987 V 42.433089 H 58.38595 V 61.620947 H 13.614042 V 42.433089 H 10.416049 V 23.24523 h 6.395987 v -3.859957 c 0,-8.017328 9.689919,-12.0307888 15.359963,-6.363975 0.418936,0.418935 0.796298,0.879444 1.125692,1.371934 l 2.702305,4.055034 2.702305,-4.055034 a 8.9863623,8.9863139 0 0 1 1.125692,-1.371934 c 5.666845,-5.6668138 15.359963,-1.653353 15.359963,6.363975 z M 23.208023,19.385273 v 3.859957 h 8.301992 l -3.536982,-5.305444 a 2.6031666,2.6031528 0 0 0 -4.76501,1.445487 z m 25.583946,0 v 3.859957 h -8.301991 l 3.536983,-5.305444 a 2.6031666,2.6031528 0 0 1 4.765008,1.442286 z m 6.395987,10.255909 v 6.395951 H 39.19799 v -6.395951 z m -3.197992,25.58381 V 42.433089 H 39.19799 V 55.224992 Z M 32.802003,29.641182 v 6.395951 H 16.812036 v -6.395951 z m 0,12.791907 H 20.010028 v 12.791903 h 12.791975 z";
        private const string GIFTED_MANY_ICON_URL = "https://static-cdn.jtvnw.net/subs-image-assets/gift-illus.png";
        private const string GIFTED_ANONYMOUS_ICON_SVG = "m 54.571425,64.514958 a 4.3531428,4.2396967 0 0 1 -1.273998,-0.86096 l -1.203426,-1.172067 a 7.0051428,6.822584 0 0 0 -9.90229,0 c -3.417139,3.328092 -8.962569,3.328092 -12.383427,0 l -0.159707,-0.155553 a 7.1871427,6.9998405 0 0 0 -9.854005,-0.28216 l -1.894286,1.635103 a 4.9362858,4.8076423 0 0 1 -3.276,1.215474 H 10 V 32.337399 a 26.000001,25.322423 0 0 1 52,0 v 32.557396 h -5.627146 c -0.627714,0 -1.240569,-0.133847 -1.801429,-0.379837 z M 35.999996,14.249955 A 18.571428,18.087444 0 0 0 17.428572,32.337399 v 22.515245 a 14.619428,14.238435 0 0 1 17.471998,2.358609 l 0.163448,0.155554 c 0.516285,0.50645 1.355715,0.50645 1.875712,0 a 14.437428,14.061179 0 0 1 17.631712,-2.11623 V 32.337399 A 18.571428,18.087444 0 0 0 35.999996,14.249955 Z M 24.857142,35.954887 a 3.7142855,3.6174889 0 1 1 7.42857,0 3.7142855,3.6174889 0 0 1 -7.42857,0 z m 18.571432,-3.617488 a 3.7142859,3.6174892 0 1 0 0,7.234978 3.7142859,3.6174892 0 0 0 0,-7.234978 z";
        private const string BIT_BADGE_TIER_NOTIFICATION_ICON_SVG = "M 14.242705,42.37453 36,11.292679 57.757295,42.37453 36,61.023641 Z M 22.566425,41.323963 36,22.13092 49.433577,41.317747 46.79162,43.580506 36,39.266345 25.205273,43.586723 22.566425,41.320854 Z";
        private const string WATCH_STREAK_ICON_SVG = "M 38.84325,21.169078 33.156748,14.060989 21.215093,27.992844 a 21.267516,21.267402 0 0 0 -5.11785,13.846557 c 0,9.752298 7.961102,17.713358 17.713453,17.713358 H 38.50206 A 17.400696,17.400602 0 0 0 55.902755,42.152157 c 0,-5.288419 -1.848114,-10.406242 -5.231581,-14.500501 L 41.686501,16.904225 Z m -13.306415,10.519973 7.619913,-9.098354 5.686502,7.108089 2.843251,-4.264854 4.606066,5.885497 a 16.945776,16.945684 0 0 1 3.923686,10.832728 c 0,5.91393 -4.407039,10.804296 -10.121973,11.600401 1.02357,-1.336321 1.592221,-2.985397 1.592221,-4.719771 0,-1.478483 -0.511786,-2.900101 -1.421626,-4.065827 l -4.264877,-5.316851 -4.264876,5.316851 c -0.90984,1.137294 -1.421625,2.587344 -1.421625,4.065827 0,1.705941 0.56865,3.355018 1.535355,4.662906 A 12.026952,12.026887 0 0 1 21.783744,41.839401 c 0,-3.72464 1.336328,-7.335548 3.753091,-10.15035 z";
        private const string CHARITY_DONATION_ICON_SVG = "M 14.211579,29.774743 23.549474,11.09897 H 48.450526 L 57.788421,29.774743 47.345541,42.829108 60.901052,60.90103 H 39.112633 L 36,57.010242 32.887368,60.90103 h -21.78842 l 13.55551,-18.071922 z m 13.185107,-12.450515 -3.112631,6.225256 h 23.43189 l -3.112632,-6.225256 z m 2.378051,12.450515 2.334473,3.112628 -3.598202,4.796559 -6.32798,-7.909187 z m 10.20943,22.255295 2.119703,2.645734 h 6.346656 l -5.12028,-6.829109 -3.342966,4.180262 z  M 23.549474,54.675772 42.225261,29.774743 h 7.59171 L 29.89613,54.675772 Z";
        private const string CHANNEL_POINT_ICON_SVG = "m 34.074833,10.317667 a 25.759205,25.759174 0 0 0 -23.83413,25.686052 25.759298,25.759267 0 0 0 51.518594,0 25.759205,25.759174 0 0 0 -27.684464,-25.686052 z m 0.329458,6.432744 a 19.319404,19.319381 0 0 1 20.915597,19.253308 19.319888,19.319865 0 0 1 -38.639776,0 19.319404,19.319381 0 0 1 17.724179,-19.253308 z M 36,23.124918 v 6.439401 a 6.4398012,6.4397935 0 0 1 6.439407,6.4394 H 48.88048 A 12.879602,12.879587 0 0 0 36,23.124918 Z";

        private static readonly Regex SubMessageRegex = new(@"^((?:\w+ )?subscribed (?:with Prime|at Tier \d)\. They've subscribed for \d{1,3} months(?:, currently on a \d{1,3} month streak)?! )(.+)$", RegexOptions.Compiled);
        private static readonly Regex GiftAnonymousRegex = new(@"^An anonymous user (?:gifted a|is gifting \d{1,4}) Tier \d", RegexOptions.Compiled);
        private static readonly Regex WatchStreakRegex = new(@"^((?:\w+ )?watched \d+ consecutive streams this month and sparked a watch streak! )(.+)$", RegexOptions.Compiled);

        private SKImage _subscribedTierIcon;
        private SKImage _subscribedPrimeIcon;
        private SKImage _giftSingleIcon;
        private SKImage _giftManyIcon;
        private SKImage _giftAnonymousIcon;
        private SKImage _bitBadgeTierNotificationIcon;
        private SKImage _watchStreakIcon;
        private SKImage _charityDonationIcon;

        private readonly string _cachePath;
        private readonly SKColor _purple;
        private readonly bool _offline;

        public HighlightIcons(string cachePath, SKColor iconPurple, bool offline)
        {
            _cachePath = Path.Combine(cachePath, "icons");
            _purple = iconPurple;
            _offline = offline;
        }

        // If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck
        public static HighlightType GetHighlightType(Comment comment)
        {
            if (comment.message.body.Length == 0)
            {
                // This likely happens due to the 7TV extension letting users bypass the IRC message trimmer
                return HighlightType.None;
            }

            var bodySpan = comment.message.body.AsSpan();
            var displayName = comment.commenter.display_name.AsSpan();
            if (bodySpan.StartsWith(displayName))
            {
                var bodyWithoutName = bodySpan[displayName.Length..];
                if (bodyWithoutName.StartsWith(" subscribed at Tier"))
                    return HighlightType.SubscribedTier;

                if (bodyWithoutName.StartsWith(" subscribed with Prime"))
                    return HighlightType.SubscribedPrime;

                if (bodyWithoutName.StartsWith(" is gifting"))
                    return HighlightType.GiftedMany;

                if (bodyWithoutName.StartsWith(" gifted a Tier"))
                    return HighlightType.GiftedSingle;

                if (bodyWithoutName.StartsWith(" is continuing the Gift Sub"))
                    return HighlightType.ContinuingGift;

                if (bodyWithoutName.StartsWith(" is paying forward the Gift they got from"))
                    return HighlightType.PayingForward;

                if (bodyWithoutName.Contains(" consecutive streams this month and sparked a watch streak! ", StringComparison.Ordinal))
                    return HighlightType.WatchStreak;

                if (bodyWithoutName.StartsWith(": Donated ") && bodyWithoutName[10..].Contains(" to support ", StringComparison.Ordinal))
                    return HighlightType.CharityDonation;

                if (bodyWithoutName.StartsWith(" converted from a"))
                {
                    // TODO: use bodyWithoutName when .NET 7
                    var convertedToMatch = Regex.Match(comment.message.body, $@"(?<=^{comment.commenter.display_name} converted from a (?:Prime|Tier \d) sub to a )(?:Prime|Tier \d)");
                    if (!convertedToMatch.Success)
                        return HighlightType.None;

                    // TODO: use ValueSpan when .NET 7
                    return convertedToMatch.Value switch
                    {
                        "Prime" => HighlightType.SubscribedPrime,
                        "Tier 1" => HighlightType.SubscribedTier,
                        "Tier 2" => HighlightType.SubscribedTier,
                        "Tier 3" => HighlightType.SubscribedTier,
                        _ => HighlightType.None
                    };
                }
            }

            if (bodySpan.Equals("bits badge tier notification ", StringComparison.Ordinal))
                return HighlightType.BitBadgeTierNotification;

            if (char.IsDigit(bodySpan[0]) && bodySpan.EndsWith(" have joined! "))
            {
                // TODO: use bodySpan when .NET 7
                if (Regex.IsMatch(comment.message.body, $@"^\d+ raiders from {comment.commenter.display_name} have joined! "))
                    return HighlightType.Raid;
            }

            const string ANONYMOUS_GIFT_ACCOUNT_ID = "274598607"; // Display name is 'AnAnonymousGifter'
            if (comment.commenter._id is ANONYMOUS_GIFT_ACCOUNT_ID && GiftAnonymousRegex.IsMatch(comment.message.body))
                return HighlightType.GiftedAnonymous;

            return HighlightType.None;
        }

        /// <returns>The requested icon or <see langword="null"/> if no icon exists for the highlight type</returns>
        /// <remarks>The <see cref="SKImage"/> returned is NOT a copy and should not be manually disposed.</remarks>
        public SKImage GetHighlightIcon(HighlightType highlightType, SKColor textColor, double fontSize)
        {
            return highlightType switch
            {
                HighlightType.SubscribedTier => _subscribedTierIcon ??= GenerateSvgIcon(SUBSCRIBED_TIER_ICON_SVG, textColor, fontSize),
                HighlightType.SubscribedPrime => _subscribedPrimeIcon ??= GenerateSvgIcon(SUBSCRIBED_PRIME_ICON_SVG, _purple, fontSize),
                HighlightType.GiftedSingle => _giftSingleIcon ??= GenerateSvgIcon(GIFTED_SINGLE_ICON_SVG, textColor, fontSize),
                HighlightType.GiftedMany => _giftManyIcon ??= GenerateGiftedManyIcon(fontSize, _cachePath, _offline),
                HighlightType.GiftedAnonymous => _giftAnonymousIcon ??= GenerateSvgIcon(GIFTED_ANONYMOUS_ICON_SVG, textColor, fontSize),
                HighlightType.BitBadgeTierNotification => _bitBadgeTierNotificationIcon ??= GenerateSvgIcon(BIT_BADGE_TIER_NOTIFICATION_ICON_SVG, textColor, fontSize),
                HighlightType.WatchStreak => _watchStreakIcon ??= GenerateSvgIcon(WATCH_STREAK_ICON_SVG, textColor, fontSize),
                HighlightType.CharityDonation => _charityDonationIcon ??= GenerateSvgIcon(CHARITY_DONATION_ICON_SVG, textColor, fontSize),
                _ => null
            };
        }

        private static SKImage GenerateGiftedManyIcon(double fontSize, string cachePath, bool offline)
        {
            //int newSize = (int)(fontSize / 0.2727); // 44*44px @ 12pt font // Doesn't work because our image sections aren't tall enough and I'm not rewriting that right now
            var finalIconSize = (int)(fontSize / 0.6); // 20x20px @ 12pt font

            if (offline)
            {
                using var offlineBitmap = new SKBitmap(finalIconSize, finalIconSize);
                using (var offlineCanvas = new SKCanvas(offlineBitmap))
                    offlineCanvas.Clear();
                offlineBitmap.SetImmutable();
                return SKImage.FromBitmap(offlineBitmap);
            }

            var taskIconBytes = PlatformHelper.GetImage(cachePath, GIFTED_MANY_ICON_URL, "gift-illus", "3", "png");
            taskIconBytes.Wait();
            using var ms = new MemoryStream(taskIconBytes.Result); // Illustration is 72x72
            using var codec = SKCodec.Create(ms);
            using var tempBitmap = SKBitmap.Decode(codec);

            var imageInfo = new SKImageInfo(finalIconSize, finalIconSize);
            using var resizedBitmap = tempBitmap.Resize(imageInfo, SKFilterQuality.High);
            resizedBitmap.SetImmutable();
            return SKImage.FromBitmap(resizedBitmap);
        }

        private static SKImage GenerateSvgIcon(string iconSvgString, SKColor iconColor, double fontSize)
        {
            using var tempBitmap = new SKBitmap(72, 72); // Icon SVG strings are scaled for 72x72
            using var tempCanvas = new SKCanvas(tempBitmap);

            using var iconPath = SKPath.ParseSvgPathData(iconSvgString);
            iconPath.FillType = SKPathFillType.EvenOdd;

            var iconPaint = new SKPaint
            {
                Color = iconColor,
                IsAntialias = true,
                LcdRenderText = true
            };

            tempCanvas.DrawPath(iconPath, iconPaint);
            var newSize = (int)(fontSize / 0.6); // 20*20px @ 12pt font
            var imageInfo = new SKImageInfo(newSize, newSize);
            var resizedBitmap = tempBitmap.Resize(imageInfo, SKFilterQuality.High);
            resizedBitmap.SetImmutable();
            return SKImage.FromBitmap(resizedBitmap);
        }

        /// <summary>
        /// Splits a comment into 2 comments based on the start index of a custom re-sub message
        /// </summary>
        /// <returns>
        /// 2 clones of <paramref name="comment"/> whose <see cref="Message.body"/> and <see cref="Message.fragments"/> contain the split re-sub details and
        /// the user's custom re-sub message if there is one, else the original <paramref name="comment"/> and null
        /// </returns>
        public static (Comment subMessage, Comment customMessage) SplitSubComment(Comment comment)
        {
            var subMessageMatch = SubMessageRegex.Match(comment.message.body);
            if (!subMessageMatch.Success)
            {
                // Return the original comment + null if there is no custom sub message
                return (comment, null);
            }

            var subMessage = subMessageMatch.Groups[1].Value;
            var customMessage = subMessageMatch.Groups[2].Value;

            // If we don't clone then both new comments reference the original commenter object, message object, fragment list, etc.
            var subMessageComment = comment.Clone();
            subMessageComment.message.body = subMessage;
            subMessageComment.message.fragments[0].text = subMessage;
            var customMessageComment = comment.Clone();
            customMessageComment.message.body = customMessage;

            // If only one fragment then we are done
            if (comment.message.fragments.Count == 1)
            {
                customMessageComment.message.fragments[0].text = customMessage;
                return (subMessageComment, customMessageComment);
            }

            subMessageComment.message.fragments.RemoveRange(1, comment.message.fragments.Count - 1);
            subMessageComment.message.emoticons.Clear();

            // Check to see if there is a custom message before the next fragment
            // i.e. Foobar subscribed with Prime. They've subscribed for 45 months! Hey PogChamp
            if (!customMessage.StartsWith(comment.message.fragments[1].text)) // If yes
            {
                customMessageComment.message.fragments[0].text = customMessage[..(customMessage.IndexOf(comment.message.fragments[1].text, StringComparison.Ordinal) - 1)];
                return (subMessageComment, customMessageComment);
            }

            customMessageComment.message.fragments.RemoveAt(0);
            return (subMessageComment, customMessageComment);
        }

        /// <summary>
        /// Splits a comment into 2 comments based on the start index of a custom re-sub message
        /// </summary>
        /// <returns>
        /// 2 clones of <paramref name="comment"/> whose <see cref="Message.body"/> and <see cref="Message.fragments"/> contain the split re-sub details and
        /// the user's custom re-sub message if there is one, else the original <paramref name="comment"/> and null
        /// </returns>
        public static (Comment subMessage, Comment customMessage) SplitWatchStreakComment(Comment comment)
        {
            var watchStreakMatch = WatchStreakRegex.Match(comment.message.body);
            if (!watchStreakMatch.Success)
            {
                // Return the original comment + null if there is no custom watch streak message
                return (comment, null);
            }

            var streakMessage = watchStreakMatch.Groups[1].Value;
            var customMessage = watchStreakMatch.Groups[2].Value;

            // If we don't clone then both new comments reference the original commenter object, message object, fragment list, etc.
            var streakMessageComment = comment.Clone();
            streakMessageComment.message.body = streakMessage;
            streakMessageComment.message.fragments[0].text = streakMessage;
            var customMessageComment = comment.Clone();
            customMessageComment.message.body = customMessage;

            // If only one fragment then we are done
            if (comment.message.fragments.Count == 1)
            {
                customMessageComment.message.fragments[0].text = customMessage;
                return (streakMessageComment, customMessageComment);
            }

            streakMessageComment.message.fragments.RemoveRange(1, comment.message.fragments.Count - 1);
            streakMessageComment.message.emoticons.Clear();

            // Check to see if there is a custom message before the next fragment
            // i.e. Foobar watched 3 consecutive streams this month and sparked a watch streak! Hey PogChamp
            if (!customMessage.StartsWith(comment.message.fragments[1].text)) // If yes
            {
                customMessageComment.message.fragments[0].text = customMessage[..(customMessage.IndexOf(comment.message.fragments[1].text, StringComparison.Ordinal) - 1)];
                return (streakMessageComment, customMessageComment);
            }

            customMessageComment.message.fragments.RemoveAt(0);
            return (streakMessageComment, customMessageComment);
        }
#region ImplementIDisposable

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
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
                    _bitBadgeTierNotificationIcon?.Dispose();

                    // Set the root references to null to explicitly tell the garbage collector that the resources have been disposed
                    _subscribedTierIcon = null;
                    _subscribedPrimeIcon = null;
                    _giftSingleIcon = null;
                    _giftManyIcon = null;
                    _giftAnonymousIcon = null;
                    _bitBadgeTierNotificationIcon = null;
                }
            }
            finally
            {
                Disposed = true;
            }
        }

#endregion
    }
}