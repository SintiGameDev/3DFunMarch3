using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Zentrale Animations-Hilfklasse fuer UI Toolkit Elemente.
/// Alle Tweens laufen ueber USS Transitions und schedule.Execute.
/// </summary>
public static class UIAnimator
{
    // ============ Fade ============

    public static void FadeIn(VisualElement el, float dauer = 0.25f, long verzoegerungMs = 0, Action onComplete = null)
    {
        el.style.opacity = 0f;
        el.style.display = DisplayStyle.Flex;

        el.schedule.Execute(() =>
        {
            TransitionSetzen(el, new[] { "opacity" }, new[] { dauer });
            el.style.opacity = 1f;

            if (onComplete != null)
                el.schedule.Execute(() => onComplete()).ExecuteLater((long)(dauer * 1000) + verzoegerungMs);
        }).ExecuteLater(verzoegerungMs + 20);
    }

    public static void FadeOut(VisualElement el, float dauer = 0.2f, long verzoegerungMs = 0, Action onComplete = null)
    {
        el.schedule.Execute(() =>
        {
            TransitionSetzen(el, new[] { "opacity" }, new[] { dauer });
            el.style.opacity = 0f;

            el.schedule.Execute(() =>
            {
                el.style.display = DisplayStyle.None;
                onComplete?.Invoke();
            }).ExecuteLater((long)(dauer * 1000));
        }).ExecuteLater(verzoegerungMs);
    }

    // ============ Slide ============

    public static void SlideInVonUnten(VisualElement el, float offsetPx = 40f, float dauer = 0.4f, long verzoegerungMs = 0)
    {
        el.style.opacity   = 0f;
        el.style.translate = new Translate(0, offsetPx, 0);
        el.style.display   = DisplayStyle.Flex;

        el.schedule.Execute(() =>
        {
            TransitionSetzen(el,
                new[] { "opacity", "translate" },
                new[] { dauer, dauer },
                EasingMode.EaseOut);
            el.style.opacity   = 1f;
            el.style.translate = new Translate(0, 0, 0);
        }).ExecuteLater(verzoegerungMs + 20);
    }

    public static void SlideInVonRechts(VisualElement el, float offsetPx = 24f, float dauer = 0.3f, long verzoegerungMs = 0)
    {
        el.style.opacity   = 0f;
        el.style.translate = new Translate(offsetPx, 0, 0);
        el.style.display   = DisplayStyle.Flex;

        el.schedule.Execute(() =>
        {
            TransitionSetzen(el,
                new[] { "opacity", "translate" },
                new[] { dauer, dauer },
                EasingMode.EaseOut);
            el.style.opacity   = 1f;
            el.style.translate = new Translate(0, 0, 0);
        }).ExecuteLater(verzoegerungMs + 20);
    }

    // ============ Scale Bounce ============

    public static void ScaleBounce(VisualElement el, float peak = 1.08f, float dauer = 0.12f)
    {
        TransitionSetzen(el, new[] { "scale" }, new[] { dauer }, EasingMode.EaseOut);
        el.style.scale = new StyleScale(new Scale(new Vector2(peak, peak)));

        el.schedule.Execute(() =>
        {
            TransitionSetzen(el, new[] { "scale" }, new[] { dauer * 1.2f }, EasingMode.EaseOut);
            el.style.scale = new StyleScale(new Scale(new Vector2(1f, 1f)));
        }).ExecuteLater((long)(dauer * 1000));
    }

    // ============ Shake (horizontal) ============

    public static void Shake(VisualElement el, float staerke = 6f)
    {
        float[] offsets = { -staerke, staerke, -staerke * 0.7f, staerke * 0.7f, -staerke * 0.4f, staerke * 0.4f, 0f };
        int i = 0;

        el.schedule.Execute(() =>
        {
            if (i < offsets.Length)
            {
                el.style.translate = new Translate(offsets[i], 0, 0);
                i++;
            }
        }).Every(38).Until(() => i >= offsets.Length);
    }

    // ============ Pulse (Deckkraft) ============

    public static IVisualElementScheduledItem Pulse(VisualElement el,
        float minAlpha = 0.55f, float maxAlpha = 1f,
        float halbPeriode = 0.8f)
    {
        bool richtungHoch = false;

        return el.schedule.Execute(() =>
        {
            TransitionSetzen(el, new[] { "opacity" }, new[] { halbPeriode }, EasingMode.EaseInOut);
            el.style.opacity = richtungHoch ? maxAlpha : minAlpha;
            richtungHoch = !richtungHoch;
        }).Every((long)(halbPeriode * 1000));
    }

    // ============ Zustandswechsel Cross-Fade ============

    public static void ZustandWechseln(VisualElement von, VisualElement zu,
        float dauerAus = 0.15f, float dauerEin = 0.25f, float offsetEin = 16f)
    {
        FadeOut(von, dauerAus, onComplete: () =>
        {
            SlideInVonUnten(zu, offsetEin, dauerEin, verzoegerungMs: 30);
        });
    }

    // ============ Hilfsmethoden ============

    private static void TransitionSetzen(VisualElement el,
        string[] properties, float[] dauern,
        EasingMode easing = EasingMode.EaseOut)
    {
        var propList    = new List<StylePropertyName>();
        var dauernList  = new List<TimeValue>();
        var easingList  = new List<EasingFunction>();

        for (int i = 0; i < properties.Length; i++)
        {
            propList.Add(new StylePropertyName(properties[i]));
            dauernList.Add(new TimeValue(dauern[i], TimeUnit.Second));
            easingList.Add(new EasingFunction(easing));
        }

        el.style.transitionProperty         = new StyleList<StylePropertyName>(propList);
        el.style.transitionDuration         = new StyleList<TimeValue>(dauernList);
        el.style.transitionTimingFunction   = new StyleList<EasingFunction>(easingList);
    }
}
