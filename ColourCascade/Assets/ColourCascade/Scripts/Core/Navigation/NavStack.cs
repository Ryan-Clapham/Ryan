using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Screen history with the navigation rules used by the whole game:
    ///  - the root (Home) is always at the bottom and can never be popped;
    ///  - showing the screen that is already on top does nothing (no duplicate entries from double taps);
    ///  - showing a screen that is already in the history returns to it instead of stacking a loop;
    ///  - Home clears the history back to the root.
    /// </summary>
    public sealed class NavStack<T>
    {
        readonly List<T> stack = new List<T>();
        readonly EqualityComparer<T> eq = EqualityComparer<T>.Default;

        public NavStack(T root)
        {
            stack.Add(root);
        }

        public T Root => stack[0];
        public T Current => stack[stack.Count - 1];
        public int Depth => stack.Count;
        public bool CanGoBack => stack.Count > 1;

        public bool Contains(T screen)
        {
            for (int i = 0; i < stack.Count; i++) if (eq.Equals(stack[i], screen)) return true;
            return false;
        }

        /// <summary>Shows a screen. Returns false if it was already the current screen.</summary>
        public bool Show(T screen)
        {
            if (eq.Equals(Current, screen)) return false;
            for (int i = 0; i < stack.Count; i++)
            {
                if (!eq.Equals(stack[i], screen)) continue;
                stack.RemoveRange(i + 1, stack.Count - i - 1);
                return true;
            }
            stack.Add(screen);
            return true;
        }

        /// <summary>Replaces the current screen (keeps the root).</summary>
        public void Replace(T screen)
        {
            if (stack.Count == 1)
            {
                Show(screen);
                return;
            }
            stack.RemoveAt(stack.Count - 1);
            Show(screen);
        }

        public bool Back()
        {
            if (stack.Count <= 1) return false;
            stack.RemoveAt(stack.Count - 1);
            return true;
        }

        public void Home()
        {
            stack.RemoveRange(1, stack.Count - 1);
        }
    }
}
