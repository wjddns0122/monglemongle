using System.Collections;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>A shopkeeper built from simple shapes: breathing, blinking, ear wiggle, hop on happy moments.</summary>
    public class KeeperActor : MonoBehaviour
    {
        public Transform Body, EyeL, EyeR, EarL, EarR, Head;
        float blinkAt, t0; Vector3 bodyScale = Vector3.one, eyeScale = Vector3.one;
        Quaternion earLRot, earRRot; Vector3 basePos; Coroutine hop;

        void Start()
        {
            t0 = Random.value * 10f; blinkAt = Time.time + Random.Range(1.5f, 4f);
            if (Body != null) bodyScale = Body.localScale;
            if (EyeL != null) eyeScale = EyeL.localScale;
            if (EarL != null) earLRot = EarL.localRotation;
            if (EarR != null) earRRot = EarR.localRotation;
            basePos = transform.localPosition;
        }

        void Update()
        {
            float t = Time.time + t0;
            if (Body != null) { float b = Mathf.Sin(t * 1.8f) * 0.02f; Body.localScale = new Vector3(bodyScale.x * (1 - b * 0.5f), bodyScale.y * (1 + b), bodyScale.z * (1 - b * 0.5f)); }
            if (EarL != null) EarL.localRotation = earLRot * Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 3f);
            if (EarR != null) EarR.localRotation = earRRot * Quaternion.Euler(0, 0, -Mathf.Sin(t * 1.1f + 1f) * 3f);
            if (Time.time > blinkAt)
            {
                StartCoroutine(Blink());
                blinkAt = Time.time + Random.Range(2.2f, 5f);
            }
            // gently face the camera
            var cam = Camera.main;
            if (cam != null && Head != null)
            {
                var d = cam.transform.position - Head.position; d.y = 0;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), Time.deltaTime * 2f);
            }
        }

        IEnumerator Blink()
        {
            if (EyeL == null || EyeR == null) yield break;
            var closed = new Vector3(eyeScale.x, eyeScale.y * 0.15f, eyeScale.z);
            EyeL.localScale = closed; EyeR.localScale = closed;
            yield return new WaitForSeconds(0.1f);
            EyeL.localScale = eyeScale; EyeR.localScale = eyeScale;
        }

        public void Hop()
        {
            if (hop != null) StopCoroutine(hop);
            hop = StartCoroutine(HopRoutine());
        }

        IEnumerator HopRoutine()
        {
            float t = 0f;
            while (t < 0.55f)
            {
                t += Time.deltaTime;
                float y = Mathf.Abs(Mathf.Sin(t / 0.55f * Mathf.PI * 2f)) * 0.07f * (1f - t / 0.55f);
                transform.localPosition = basePos + Vector3.up * y;
                yield return null;
            }
            transform.localPosition = basePos; hop = null;
        }
    }
}
