using System;

namespace ColourCascade.Core
{
    public struct LevelWinRecord
    {
        public bool FirstClear;
        public int PreviousStars;
        public int Stars;
        public int PreviousBest;
        public bool NewBest;
        public bool UnlockedNext;
    }

    /// <summary>Campaign unlocks, stars and best scores. Replays can only ever improve a record.</summary>
    public sealed class CampaignProgress
    {
        readonly SaveData data;

        public CampaignProgress(SaveData data)
        {
            this.data = data;
        }

        public event Action Changed;

        public int HighestUnlocked => data.HighestUnlocked;
        /// <summary>The level "Continue" plays: the furthest unlocked level.</summary>
        public int CurrentLevel => data.HighestUnlocked;

        public bool IsUnlocked(int level) => level >= 1 && level <= data.HighestUnlocked;

        public bool IsCompleted(int level)
        {
            var r = data.FindLevel(level);
            return r != null && r.Completed;
        }

        public int StarsFor(int level)
        {
            var r = data.FindLevel(level);
            return r != null ? r.Stars : 0;
        }

        public int BestScore(int level)
        {
            var r = data.FindLevel(level);
            return r != null ? r.BestScore : 0;
        }

        public int TotalStars
        {
            get
            {
                int n = 0;
                foreach (var r in data.Levels) n += r.Stars;
                return n;
            }
        }

        public int StarsInRange(int first, int last)
        {
            int n = 0;
            foreach (var r in data.Levels)
                if (r.Level >= first && r.Level <= last) n += r.Stars;
            return n;
        }

        public LevelWinRecord RecordWin(int level, int finalScore, int stars)
        {
            var r = data.GetOrCreateLevel(level);
            var result = new LevelWinRecord
            {
                FirstClear = !r.Completed,
                PreviousStars = r.Stars,
                PreviousBest = r.BestScore,
            };
            r.Plays++;
            r.Wins++;
            r.Completed = true;
            r.Stars = Math.Max(r.Stars, Math.Max(0, Math.Min(3, stars)));
            if (finalScore > r.BestScore)
            {
                result.NewBest = r.BestScore > 0;
                r.BestScore = finalScore;
            }
            result.Stars = r.Stars;
            if (level + 1 > data.HighestUnlocked)
            {
                data.HighestUnlocked = level + 1;
                result.UnlockedNext = true;
            }
            Changed?.Invoke();
            return result;
        }

        public void RecordAttempt(int level, int score)
        {
            var r = data.GetOrCreateLevel(level);
            r.Plays++;
            Changed?.Invoke();
        }
    }
}
