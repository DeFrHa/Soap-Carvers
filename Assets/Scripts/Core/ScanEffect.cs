using System;
using System.Collections;
using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Core
{
    /// <summary>
    /// The end-of-round "scanner": a glowing translucent plane (plus a cyan light)
    /// sweeps from the top of the block to the bottom, then reports completion.
    /// </summary>
    public class ScanEffect : MonoBehaviour
    {
        [SerializeField] SoapBlock soap;
        [SerializeField] Transform scanPlane;
        [SerializeField] Light scanLight;

        Coroutine _routine;

        public bool IsPlaying => _routine != null;

        public void Configure(SoapBlock soapBlock, Transform plane, Light light)
        {
            soap = soapBlock;
            scanPlane = plane;
            scanLight = light;
        }

        void Awake()
        {
            SetVisible(false);
        }

        public void Play(float seconds, Action onComplete)
        {
            Stop();
            _routine = StartCoroutine(Sweep(seconds, onComplete));
        }

        public void Stop()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            SetVisible(false);
        }

        IEnumerator Sweep(float seconds, Action onComplete)
        {
            float size = soap != null ? soap.Grid.Extent : 16f;
            Vector3 basePos = soap != null ? soap.transform.position : transform.position;
            float top = basePos.y + size + 0.5f;
            float bottom = basePos.y - 0.1f;
            if (scanPlane != null)
                scanPlane.localScale = new Vector3(size + 2f, 0.04f, size + 2f);
            SetVisible(true);

            float t = 0f;
            seconds = Mathf.Max(0.1f, seconds);
            while (t < 1f)
            {
                t += Time.deltaTime / seconds;
                // Ease in-out so it lingers a bit at the start and end.
                float e = Mathf.SmoothStep(0f, 1f, t);
                float y = Mathf.Lerp(top, bottom, e);
                if (scanPlane != null) scanPlane.position = new Vector3(basePos.x, y, basePos.z);
                if (scanLight != null)
                {
                    scanLight.transform.position = new Vector3(basePos.x, y + 0.5f, basePos.z);
                    scanLight.intensity = 6f + Mathf.Sin(Time.time * 25f) * 2f;
                }
                yield return null;
            }

            SetVisible(false);
            _routine = null;
            onComplete?.Invoke();
        }

        void SetVisible(bool visible)
        {
            if (scanPlane != null) scanPlane.gameObject.SetActive(visible);
            if (scanLight != null) scanLight.enabled = visible;
        }
    }
}
