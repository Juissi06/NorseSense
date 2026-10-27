using UnityEngine;

namespace NorseSense
{
    // How every Norse Sense panel moves: it fades and slides in a little when it appears, and eases to its new
    // size instead of jumping when rows come and go. One per panel; call Step with where the panel should be.
    internal class PanelMotion
    {
        const float Speed = 16f, FadeTime = 0.15f, Slide = 6f;

        Rect _rect;
        float _lastDrawn = -10f, _lastStep;
        internal float Alpha { get; private set; }

        internal Rect Step(Rect target)
        {
            float now = Time.unscaledTime;
            if (now - _lastDrawn > 0.25f)
            {
                _rect = new Rect(target.x, target.y + Slide, target.width, target.height);
                Alpha = 0f;
                _lastStep = now;
            }
            _lastDrawn = now;
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(now - _lastStep, 0.1f);
                _lastStep = now;
                float k = 1f - Mathf.Exp(-Speed * dt);
                _rect.x = Mathf.Lerp(_rect.x, target.x, k);
                _rect.y = Mathf.Lerp(_rect.y, target.y, k);
                _rect.width = Mathf.Lerp(_rect.width, target.width, k);
                _rect.height = Mathf.Lerp(_rect.height, target.height, k);
                Alpha = Mathf.MoveTowards(Alpha, 1f, dt / FadeTime);
            }
            return _rect;
        }
    }
}
