using UnityEngine;

public class PickupFloatRotate : MonoBehaviour
{
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;
    [SerializeField] private float _rotationSpeedDegrees = 70f;
    [SerializeField] private float _bobAmplitude = 0.08f;
    [SerializeField] private float _bobFrequency = 1.6f;

    private Vector3 _startLocalPosition;
    private float _bobOffset;

    private void Awake()
    {
        _startLocalPosition = transform.localPosition;
        _bobOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        transform.Rotate(_rotationAxis.normalized, _rotationSpeedDegrees * Time.deltaTime, Space.Self);

        Vector3 localPosition = _startLocalPosition;
        localPosition.y += Mathf.Sin(Time.time * _bobFrequency + _bobOffset) * _bobAmplitude;
        transform.localPosition = localPosition;
    }
}
