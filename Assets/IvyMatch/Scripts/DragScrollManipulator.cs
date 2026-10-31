using UnityEngine;
using UnityEngine.UIElements;

namespace IvyMatch
{
    /// <summary>
    /// Drag-to-scroll for a ScrollView with mouse/pen pointers (Game view, Device Simulator on desktop).
    /// Real touch is left to ScrollView's built-in touch scrolling.
    /// A press only becomes a drag after moving DragThreshold px, so taps still reach the cards;
    /// once dragging, the pointer is captured, so the release does not fire a ClickEvent on a card.
    /// </summary>
    public class DragScrollManipulator : PointerManipulator
    {
        public const float DragThreshold = 10f;

        // Inertia after a mouse fling (touch already gets ScrollView's own inertia).
        const float MinFlingSpeed = 0.15f;   // px per ms
        const float StopSpeed = 0.02f;       // px per ms
        const float FrictionPer16ms = 0.94f; // velocity kept per 16 ms frame
        const long StaleMs = 80;             // a pause this long before release = no fling

        readonly ScrollView scrollView;
        int pointerId = -1;
        Vector2 startPointer;
        Vector2 startOffset;
        bool dragging;

        float velocity;                      // scroll offset change, px per ms (+ = content moves up)
        float lastOffsetY;
        long lastMoveTime;
        IVisualElementScheduledItem inertia;

        public DragScrollManipulator(ScrollView scrollView)
        {
            this.scrollView = scrollView;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            // TrickleDown: see presses that start on cards/buttons inside the list.
            target.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        void OnDown(PointerDownEvent e)
        {
            if (e.pointerType == UnityEngine.UIElements.PointerType.touch || e.button != 0) return;
            StopInertia();
            pointerId = e.pointerId;
            startPointer = e.position;
            startOffset = scrollView.scrollOffset;
            dragging = false;
            velocity = 0f;
            lastOffsetY = startOffset.y;
            lastMoveTime = e.timestamp;
        }

        void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != pointerId) return;

            Vector2 delta = (Vector2)e.position - startPointer;
            if (!dragging)
            {
                if (Mathf.Abs(delta.y) < DragThreshold) return;
                dragging = true;
                target.CapturePointer(pointerId);
            }

            float y = Mathf.Clamp(startOffset.y - delta.y, 0f, MaxOffset());
            scrollView.scrollOffset = new Vector2(startOffset.x, y);

            // Smoothed fling speed from the last moves.
            long dt = e.timestamp - lastMoveTime;
            if (dt > 0)
            {
                float v = (y - lastOffsetY) / dt;
                velocity = 0.7f * v + 0.3f * velocity;
                lastOffsetY = y;
                lastMoveTime = e.timestamp;
            }
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != pointerId) return;
            if (dragging)
            {
                e.StopPropagation();
                if (target.HasPointerCapture(pointerId)) target.ReleasePointer(pointerId);
                bool stale = e.timestamp - lastMoveTime > StaleMs;
                if (!stale && Mathf.Abs(velocity) >= MinFlingSpeed) StartInertia();
            }
            Reset();
        }

        float MaxOffset()
        {
            return Mathf.Max(0f, scrollView.contentContainer.layout.height - scrollView.contentViewport.layout.height);
        }

        void StartInertia()
        {
            StopInertia();
            inertia = scrollView.schedule.Execute(StepInertia).Every(16);
        }

        void StepInertia(TimerState t)
        {
            float dt = Mathf.Max(1f, t.deltaTime);
            float max = MaxOffset();
            float y = scrollView.scrollOffset.y + velocity * dt;
            if (y <= 0f || y >= max)
            {
                y = Mathf.Clamp(y, 0f, max);
                velocity = 0f;
            }
            scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, y);
            velocity *= Mathf.Pow(FrictionPer16ms, dt / 16f);
            if (Mathf.Abs(velocity) < StopSpeed) StopInertia();
        }

        void StopInertia()
        {
            if (inertia != null) inertia.Pause();
            inertia = null;
        }

        void OnCaptureOut(PointerCaptureOutEvent e) => Reset();

        void Reset()
        {
            pointerId = -1;
            dragging = false;
        }
    }
}
