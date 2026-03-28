using UnityEngine;

public class NextbotAgent : MonoBehaviour
{
    [Header("Network Smoothing")]
    [SerializeField] private float positionLerpSpeed = 12f;
    [SerializeField] private float rotationLerpSpeed = 14f;

    [Header("Visual")]
    [SerializeField] private Texture2D faceTexture;
    [SerializeField] private Vector3 faceLocalPosition = new Vector3(0f, 1.9f, 0f);
    [SerializeField] private Vector2 faceScale = new Vector2(3f, 3f);
    [SerializeField] private Color faceTint = Color.white;

    private Transform _faceTransform;
    private SpriteRenderer _faceRenderer;
    private SphereCollider _bodyCollider;
    private Texture2D _runtimeFaceTexture;
    private Sprite _runtimeFaceSprite;
    private bool _isVisible;
    private bool _hasAppliedServerState;

    private void Awake()
    {
        EnsureFaceRenderer();
        SetVisualActive(false);
    }

    private void Update()
    {
        UpdateFromRoomState();
    }

    private void LateUpdate()
    {
        if (_isVisible)
        {
            UpdateBillboard();
        }
    }

    private void OnDestroy()
    {
        if (_runtimeFaceSprite != null)
        {
            Destroy(_runtimeFaceSprite);
        }

        if (_runtimeFaceTexture != null)
        {
            Destroy(_runtimeFaceTexture);
        }
    }

    private void UpdateFromRoomState()
    {
        NextbotState nextbotState = GetNextbotState();
        if (nextbotState == null || !nextbotState.isActive)
        {
            SetVisualActive(false);
            _hasAppliedServerState = false;
            return;
        }

        EnsureFaceRenderer();
        SetVisualActive(true);

        Vector3 targetPosition = new Vector3(nextbotState.x, nextbotState.y, nextbotState.z);
        Quaternion targetRotation = Quaternion.Euler(0f, nextbotState.rotationY, 0f);

        if (!_hasAppliedServerState)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            _hasAppliedServerState = true;
            return;
        }

        float positionBlend = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
    }

    private NextbotState GetNextbotState()
    {
        NetworkManager networkManager = NetworkManager.Instance;
        if (networkManager == null || networkManager.Room == null || networkManager.Room.State == null)
        {
            return null;
        }

        return networkManager.Room.State.nextbot;
    }

    private void EnsureFaceRenderer()
    {
        if (_faceRenderer != null)
        {
            return;
        }

        GameObject faceObject = new GameObject("NextbotFace");
        faceObject.transform.SetParent(transform, false);
        faceObject.transform.localPosition = faceLocalPosition;
        faceObject.transform.localRotation = Quaternion.identity;
        faceObject.transform.localScale = new Vector3(faceScale.x, faceScale.y, 1f);

        _faceTransform = faceObject.transform;
        _faceRenderer = faceObject.AddComponent<SpriteRenderer>();
        _faceRenderer.sprite = CreateFaceSprite();
        _faceRenderer.color = faceTint;
        _faceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _faceRenderer.receiveShadows = false;

        _bodyCollider = gameObject.GetComponent<SphereCollider>();
        if (_bodyCollider == null)
        {
            _bodyCollider = gameObject.AddComponent<SphereCollider>();
        }

        _bodyCollider.radius = 0.6f;
        _bodyCollider.center = new Vector3(0f, 1f, 0f);
    }

    private void SetVisualActive(bool isVisible)
    {
        _isVisible = isVisible;

        if (_faceRenderer != null)
        {
            _faceRenderer.enabled = isVisible;
        }

        if (_bodyCollider != null)
        {
            _bodyCollider.enabled = isVisible;
        }
    }

    private Sprite CreateFaceSprite()
    {
        Texture2D sourceTexture = faceTexture;
        if (sourceTexture == null)
        {
            _runtimeFaceTexture = GenerateDefaultFaceTexture();
            sourceTexture = _runtimeFaceTexture;
        }

        _runtimeFaceSprite = Sprite.Create(
            sourceTexture,
            new Rect(0f, 0f, sourceTexture.width, sourceTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return _runtimeFaceSprite;
    }

    private void UpdateBillboard()
    {
        if (_faceTransform == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled)
                {
                    targetCamera = cameras[i];
                    break;
                }
            }
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        cameraPosition.y = _faceTransform.position.y;
        Vector3 awayFromCamera = _faceTransform.position - cameraPosition;
        if (awayFromCamera.sqrMagnitude > 0.001f)
        {
            _faceTransform.forward = awayFromCamera.normalized;
        }
    }

    private Texture2D GenerateDefaultFaceTexture()
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;

        Color background = new Color(0.93f, 0.93f, 0.93f, 1f);
        Color outline = new Color(0.1f, 0.1f, 0.1f, 1f);
        Color eyeWhite = Color.white;
        Color eyePupil = new Color(0.65f, 0.1f, 0.1f, 1f);
        Color mouth = new Color(0.12f, 0.12f, 0.12f, 1f);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        texture.SetPixels(pixels);

        FillRect(texture, 14, 14, size - 28, size - 28, outline);
        FillRect(texture, 22, 22, size - 44, size - 44, background);

        DrawCircle(texture, 85, 150, 38, outline);
        DrawCircle(texture, 170, 150, 38, outline);
        DrawCircle(texture, 85, 150, 30, eyeWhite);
        DrawCircle(texture, 170, 150, 30, eyeWhite);
        DrawCircle(texture, 95, 145, 10, eyePupil);
        DrawCircle(texture, 180, 145, 10, eyePupil);

        FillRect(texture, 82, 58, 96, 18, mouth);
        FillRect(texture, 76, 76, 108, 8, mouth);

        texture.Apply();
        return texture;
    }

    private void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    private void DrawCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
    {
        int radiusSquared = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radiusSquared)
                {
                    texture.SetPixel(centerX + x, centerY + y, color);
                }
            }
        }
    }
}
