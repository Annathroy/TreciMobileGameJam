using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DisableFlashEffect : MonoBehaviour
{
    [Header("UI Setup")]
    private Canvas canvas; 
    [SerializeField] private Sprite imageSprite1;
    [SerializeField] private Sprite imageSprite2;

    [Header("Timing")]
    [SerializeField] private float eachLifetime = 0.1f;
    [SerializeField] private float gapBetween = 0.1f;
    private void Awake()
    {
        canvas = FindAnyObjectByType<Canvas>();
    }
    private void OnDisable()
    {
        if (!canvas || !imageSprite1 || !imageSprite2) return;
        StartCoroutine(SpawnTwoImages());
    }

    private IEnumerator SpawnTwoImages()
    {
        // 1st image
        Image img1 = CreateTempImage(imageSprite1);
        yield return new WaitForSeconds(eachLifetime);
        if (img1) Destroy(img1.gameObject);

        // Wait, then 2nd image
        yield return new WaitForSeconds(gapBetween);

        Image img2 = CreateTempImage(imageSprite2);
        yield return new WaitForSeconds(eachLifetime);
        if (img2) Destroy(img2.gameObject);
    }

    private Image CreateTempImage(Sprite sprite)
    {
        GameObject go = new GameObject("TempImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.SetNativeSize();

        // optional center position (adjust as you wish)
        RectTransform rt = img.rectTransform;
        rt.anchoredPosition = Vector2.zero;

        return img;
    }
}
