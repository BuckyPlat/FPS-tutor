using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MouseSettings : MonoBehaviour
{
    [Header("UI References")]
    public Slider horizontalLookSlider;
    public Slider verticalLookSlider;
    public Slider smoothingSlider;
    public Button invertMouseButton;
    public TextMeshPro invertButtonText;

    [Header("Save Button (tùy chọn)")]
    public Button saveButton;        // Drag "Save & Apply" or "Apply" button here

    private const string SENS_X_KEY = "MouseSensX";
    private const string SENS_Y_KEY = "MouseSensY";
    private const string SMOOTH_KEY = "MouseSmooth";
    private const string INVERT_KEY = "MouseInvert";

    private void Start()
    {
        // Load saved values (if any) into sliders
        horizontalLookSlider.value = PlayerPrefs.GetFloat(SENS_X_KEY, 2f);
        verticalLookSlider.value = PlayerPrefs.GetFloat(SENS_Y_KEY, 2f);
        smoothingSlider.value = PlayerPrefs.GetFloat(SMOOTH_KEY, 3f);

        bool savedInvert = PlayerPrefs.GetInt(INVERT_KEY, 0) == 1;
        UpdateInvertButtonUI(savedInvert);

        // Register sliders
        horizontalLookSlider.onValueChanged.AddListener(OnHorizontalChanged);
        verticalLookSlider.onValueChanged.AddListener(OnVerticalChanged);
        smoothingSlider.onValueChanged.AddListener(OnSmoothingChanged);

        if (invertMouseButton != null)
            invertMouseButton.onClick.AddListener(OnInvertButtonClicked);

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveSettings);
        else
            SaveSettings(); // Auto-save on slider drag (more user friendly)
    }

    private void OnHorizontalChanged(float value) => SaveSettings();
    private void OnVerticalChanged(float value) => SaveSettings();
    private void OnSmoothingChanged(float value) => SaveSettings();

    public void OnInvertButtonClicked()
    {
        bool current = PlayerPrefs.GetInt(INVERT_KEY, 0) == 1;
        bool newValue = !current;
        PlayerPrefs.SetInt(INVERT_KEY, newValue ? 1 : 0);
        UpdateInvertButtonUI(newValue);
        SaveSettings();
    }

    private void UpdateInvertButtonUI(bool isInverted)
    {
        if (invertButtonText != null)
            invertButtonText.text = isInverted ? "on" : "off";
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(SENS_X_KEY, horizontalLookSlider.value);
        PlayerPrefs.SetFloat(SENS_Y_KEY, verticalLookSlider.value);
        PlayerPrefs.SetFloat(SMOOTH_KEY, smoothingSlider.value);
        // Invert is already saved in OnInvertButtonClicked
        PlayerPrefs.Save();
    }
}