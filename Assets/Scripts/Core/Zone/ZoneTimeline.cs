using System;

namespace WarOfNyx.Core.Zone
{
    /// <summary>
    /// The circle's state for one phase, as replicated by the server: where this phase starts and
    /// ends, and when. Every peer derives the current circle from it with <see cref="CircleAt"/>,
    /// so clients see a smooth shrink without the server sending the radius every tick.
    /// </summary>
    public struct ZoneStage
    {
        /// <summary>0-based phase index; equal to the stage count once the final circle has closed.</summary>
        public int Index;
        public int Count;
        public ZoneCircle From;
        public ZoneCircle To;
        /// <summary>Times in the same clock as the caller (server simulation seconds when networked).</summary>
        public float StartsAt;
        public float EndsAt;
        public float DamagePerSecond;
        /// <summary>When the final phase starts (for the "FINAL CIRCLE BEGINS IN 10 SECONDS" warning).</summary>
        public float FinalStartsAt;

        public bool Finished => Index >= Count;
        public bool IsFinal => Count > 0 && Index == Count - 1;

        /// <summary>Linear shrink from <see cref="From"/> to <see cref="To"/> between the phase's start and end.</summary>
        public ZoneCircle CircleAt(float time)
        {
            if (Finished || EndsAt <= StartsAt)
                return time >= EndsAt ? To : From;
            return ZoneCircle.Lerp(From, To, (time - StartsAt) / (EndsAt - StartsAt));
        }

        public bool Shrinking(float time) => !Finished && time >= StartsAt && time < EndsAt;

        public float SecondsRemaining(float time) => Finished ? 0f : Math.Max(0f, (time < StartsAt ? StartsAt : EndsAt) - time);
    }

    /// <summary>
    /// Server-side schedule (spec 10): 60-second phases one after another from the match start.
    /// During phase i the circle shrinks continuously from circle i to circle i+1; at the end of
    /// the phase the shrink is complete and the next phase starts. After the final phase the
    /// circle stays closed (radius 0) until the match ends.
    /// </summary>
    public static class ZoneTimeline
    {
        /// <param name="matchStart">Server time of "FIGHT!".</param>
        public static ZoneStage Evaluate(ZonePlan plan, ZoneSettings settings, float matchStart, float now)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            int count = Math.Min(plan.StageCount, settings.StageCount);
            float t = matchStart + Math.Max(0f, settings.delayBeforeFirstPhase);
            float finalStartsAt = t;
            for (int i = 0; i < count - 1; i++)
                finalStartsAt += settings.stages[i].durationSeconds;

            for (int i = 0; i < count; i++)
            {
                float end = t + settings.stages[i].durationSeconds;
                // Before the first phase starts (delay), phase 0 is reported as not yet shrinking.
                if (now < end)
                {
                    return new ZoneStage
                    {
                        Index = i,
                        Count = count,
                        From = plan.Circles[i],
                        To = plan.Circles[i + 1],
                        StartsAt = t,
                        EndsAt = end,
                        DamagePerSecond = settings.stages[i].damagePerSecond,
                        FinalStartsAt = finalStartsAt,
                    };
                }
                t = end;
            }

            var closed = plan.Circles[count];
            return new ZoneStage
            {
                Index = count,
                Count = count,
                From = closed,
                To = closed,
                StartsAt = t,
                EndsAt = t,
                DamagePerSecond = count > 0 ? settings.stages[count - 1].damagePerSecond : 0f,
                FinalStartsAt = finalStartsAt,
            };
        }
    }
}
