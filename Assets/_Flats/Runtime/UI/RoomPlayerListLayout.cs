using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(VerticalLayoutGroup))]
public sealed class RoomPlayerListLayout : MonoBehaviour
{
    void OnEnable()
    {
        StartCoroutine(RefreshAfterActivation());
    }

    IEnumerator RefreshAfterActivation()
    {
        // MatchingDetails is activated by the menu Animator. Rows may already
        // exist while it is hidden; refresh after that activation has settled.
        yield return null;
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }
}
