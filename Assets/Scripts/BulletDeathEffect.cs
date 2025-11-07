using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class BulletDeathEffect : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Sprite imageSprite1;
    [SerializeField] private Sprite imageSprite2;
    [SerializeField] private float gapBetween = 0.1f;
    [SerializeField] private float lifetime = 0.1f;

    private Canvas canvas;
    private Camera mainCam;
    private bool isDead;

    private void Awake()
    {
        canvas = FindAnyObjectByType<Canvas>();
        mainCam = Camera.main;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        if (!other.CompareTag("Projectile")) return;

        isDead = true;

        // capture world pos before disabling
        Vector3 deathPos = transform.position;

        // run the effect from a global runner so timing still works after disable
        DelayedRunner.Run(DeathEffect(deathPos));

        // pooled-friendly
        gameObject.SetActive(false);
    }

    private IEnumerator DeathEffect(Vector3 worldPos)
    {
        if (!canvas) yield break;

        // spawn first image
        Image img1 = CreateTempImage(imageSprite1, worldPos);

        // add score WHEN first image actually appears
        if (img1 != null)
            Score.Add(10);   // change to 5 if you want +5 here

        // let first image live
        yield return new WaitForSeconds(lifetime);
        if (img1) Destroy(img1.gameObject);

        // gap then second image
        yield return new WaitForSeconds(gapBetween);
        Image img2 = CreateTempImage(imageSprite2, worldPos);
        yield return new WaitForSeconds(lifetime);
        if (img2) Destroy(img2.gameObject);
    }

    private Image CreateTempImage(Sprite sprite, Vector3 worldPos)
    {
        if (!sprite || !canvas) return null;

        var go = new GameObject("DeathImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.SetNativeSize();

        var rt = img.rectTransform;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 screenPos = mainCam ? (Vector2)mainCam.WorldToScreenPoint(worldPos) : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, null, out var localPos);
            rt.anchoredPosition = localPos;
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Vector2 screenPos = mainCam ? (Vector2)mainCam.WorldToScreenPoint(worldPos) : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, canvas.worldCamera, out var localPos);
            rt.anchoredPosition = localPos;
        }
        else // WorldSpace
        {
            go.transform.position = worldPos;
        }

        return img;
    }
}
