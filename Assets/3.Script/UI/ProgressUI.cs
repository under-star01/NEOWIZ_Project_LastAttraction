using UnityEngine;
using UnityEngine.UI;

public class ProgressUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image fillImage;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        SetProgress(0f);
    }

    public void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);

        if (fillImage != null)
            fillImage.fillAmount = value;
    }
}