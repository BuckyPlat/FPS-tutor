using UnityEngine;
using UnityEngine.UIElements;

public class SliderDebugger : MonoBehaviour
{
void Start() { var doc = GetComponent<UIDocument>(); if (doc == null) { Debug.Log("SD: No UIDocument"); return; } doc.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometry); }

void OnGeometry(GeometryChangedEvent evt) { var doc = GetComponent<UIDocument>(); if (doc == null) return; var root = doc.rootVisualElement; var dc = root.Q(className: "unity-base-slider__drag-container"); if (dc == null || dc.resolvedStyle.height == 0) return; root.UnregisterCallback<GeometryChangedEvent>(OnGeometry); var tracker = dc.Q(className: "unity-base-slider__tracker"); var fill = dc.Q(className: "unity-base-slider__fill"); var dragger = dc.Q(className: "unity-base-slider__dragger"); Debug.Log($"SD_DC h={dc.resolvedStyle.height} pos={dc.resolvedStyle.position} align={dc.resolvedStyle.alignItems} layout={dc.layout}"); if (tracker != null) Debug.Log($"SD_TRACKER layout={tracker.layout} top={tracker.resolvedStyle.top} pos={tracker.resolvedStyle.position} h={tracker.resolvedStyle.height}"); if (fill != null) Debug.Log($"SD_FILL layout={fill.layout} top={fill.resolvedStyle.top} pos={fill.resolvedStyle.position} h={fill.resolvedStyle.height}"); if (dragger != null) Debug.Log($"SD_DRAGGER layout={dragger.layout} top={dragger.resolvedStyle.top} pos={dragger.resolvedStyle.position} h={dragger.resolvedStyle.height}"); }


    void Inspect()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        var dc = root.Q(className: "unity-base-slider__drag-container");
        if (dc == null) { Debug.Log("SLIDER_DEBUG: No drag-container found"); return; }

        var tracker = dc.Q(className: "unity-base-slider__tracker");
        var fill    = dc.Q(className: "unity-base-slider__fill");
        var dragger = dc.Q(className: "unity-base-slider__dragger");

        Debug.Log($"SLIDER_DEBUG DC: h={dc.resolvedStyle.height} pos={dc.resolvedStyle.position} align={dc.resolvedStyle.alignItems} layout={dc.layout}");
        if (tracker != null) Debug.Log($"SLIDER_DEBUG TRACKER: layout={tracker.layout} top={tracker.resolvedStyle.top} pos={tracker.resolvedStyle.position}");
        if (fill    != null) Debug.Log($"SLIDER_DEBUG FILL: layout={fill.layout} top={fill.resolvedStyle.top} pos={fill.resolvedStyle.position}");
        if (dragger != null) Debug.Log($"SLIDER_DEBUG DRAGGER: layout={dragger.layout} top={dragger.resolvedStyle.top} pos={dragger.resolvedStyle.position}");
    }
}
