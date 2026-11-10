using System;
using System.Collections.Generic;

namespace GrowWhatever.Core
{
    [Serializable]
    public struct DisplayChoice : IEquatable<DisplayChoice>
    {
        public int Width, Height;
        public bool Borderless;
        public DisplayChoice(int width, int height, bool borderless = false)
        { Width = width; Height = height; Borderless = borderless; }
        public bool Equals(DisplayChoice other) => Width == other.Width && Height == other.Height && Borderless == other.Borderless;
        public override bool Equals(object obj) => obj is DisplayChoice other && Equals(other);
        public override int GetHashCode() => Width ^ (Height << 8) ^ (Borderless ? 1 : 0);
    }

    [Serializable]
    public sealed class PlayerPreferences
    {
        public int Version = 1;
        public DisplayChoice Display = new DisplayChoice(1280, 720);
        public float Master = 1, Music = 0.7f, Effects = 0.7f;

        public static DisplayChoice[] AvailableResolutions(int width, int height)
        {
            var choices = new List<DisplayChoice>();
            foreach (var size in new[] { new DisplayChoice(1280,720), new DisplayChoice(1600,900),
                new DisplayChoice(1920,1080), new DisplayChoice(2560,1440), new DisplayChoice(3840,2160) })
                if (size.Width <= width && size.Height <= height) choices.Add(size);
            return choices.ToArray();
        }

        public static DisplayChoice Fit(DisplayChoice choice, int width, int height)
        {
            var sizes = AvailableResolutions(width, height);
            if (sizes.Length == 0) return new DisplayChoice(1280, 720, true);
            bool known = Array.Exists(AvailableResolutions(3840,2160), x => x.Width == choice.Width && x.Height == choice.Height);
            if (!known) choice = new DisplayChoice(1280, 720, choice.Borderless);
            DisplayChoice fit = sizes[0];
            foreach (var size in sizes)
                if (size.Width <= choice.Width && size.Height <= choice.Height) fit = size;
            fit.Borderless = choice.Borderless;
            return fit;
        }

        public bool Normalize(int width, int height)
        {
            var display = Fit(Display, width, height);
            float master = ClampVolume(Master, 1), music = ClampVolume(Music, 0.7f), effects = ClampVolume(Effects, 0.7f);
            bool changed = !display.Equals(Display) || master != Master || music != Music || effects != Effects;
            Display = display; Master = master; Music = music; Effects = effects;
            return changed;
        }

        public static float ClampVolume(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value)
            ? fallback : Math.Max(0, Math.Min(1, value));
    }

    /// <summary>Only confirmed display choices may be persisted; audio is independent of this transaction.</summary>
    public sealed class DisplayPreview
    {
        public DisplayChoice Confirmed { get; private set; }
        public DisplayChoice Pending { get; private set; }
        public bool IsActive { get; private set; }
        public double Deadline { get; private set; }
        public DisplayPreview(DisplayChoice confirmed) { Confirmed = Pending = confirmed; }
        public void Begin(DisplayChoice choice, double now)
        {
            if (IsActive) throw new InvalidOperationException("A display preview is already active.");
            Pending = choice; Deadline = now + 15; IsActive = true;
        }
        public bool Keep(double now)
        {
            if (!IsActive || now >= Deadline) { Cancel(); return false; }
            Confirmed = Pending; IsActive = false; return true;
        }
        public bool Expire(double now)
        {
            if (!IsActive || now < Deadline) return false;
            Cancel(); return true;
        }
        public void Cancel() { Pending = Confirmed; IsActive = false; }
    }
}
