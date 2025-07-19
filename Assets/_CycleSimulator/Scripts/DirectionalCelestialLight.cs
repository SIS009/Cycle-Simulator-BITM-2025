using System;
using UnityEngine;
using Cinemachine;

namespace SuperSimpleSkybox
{
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Super Simple Skybox/Directional Celestial Light")]
    public class DirectionalCelestialLight : MonoBehaviour
    {
        public enum CelestialType { Sun, Moon }

        [Header("Light Type")]
        [SerializeField] private CelestialType celestialType = CelestialType.Sun;

        [Header("Cart Controlled Transition")]
        [SerializeField] private CinemachineDollyCart dollyCart;
        [SerializeField] private float cartMinPos = 0.1f;
        [SerializeField] private float cartMaxPos = 2490f;

        [Header("Light Intensity Settings")]
        [SerializeField] private float maximumLightIntensity = 2f;
        [SerializeField] private float smoothingSpeed = 3f;

        private Light _light;
        private float currentIntensity = 0f;
        private float intensityVelocity = 0f;

        public enum LightState { Down, Up }
        private LightState state;
        public LightState State => state;

        public event Action OnRise;
        public event Action OnSet;

        private Vector3 startEulerAngles;
        private Vector3 endEulerAngles;

        private void OnEnable()
        {
            _light = GetComponent<Light>();
            InitRotationValues();
            UpdateLightState();
        }

        private void InitRotationValues()
        {
            if (celestialType == CelestialType.Sun)
            {
                startEulerAngles = new Vector3(11.261f, -167.312f, 11.502f);
                endEulerAngles = new Vector3(190f, 20f, 0f); // example sunset direction
            }
            else if (celestialType == CelestialType.Moon)
            {
                startEulerAngles = new Vector3(-17.239f, -20.184f, 24.099f);
                endEulerAngles = new Vector3(-20f, 180f, 0f); // example moonset direction
            }
        }

        private void Update()
        {
            if (CyclingManager.Instance == null || dollyCart == null || !CyclingManager.Instance.isCycling)
                return;

            float pos = Mathf.Clamp(dollyCart.m_Position, cartMinPos, cartMaxPos);
            float t = Mathf.InverseLerp(cartMinPos, cartMaxPos, pos); // normalized 0–1

            // Calculate smoothed rotation
            Vector3 targetEuler = Vector3.Lerp(startEulerAngles, endEulerAngles, t);
            Quaternion targetRotation = Quaternion.Euler(targetEuler);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothingSpeed);

            // Calculate smoothed intensity based on elevation (X)
            float elevation = targetEuler.x;
            float targetIntensity = Mathf.Clamp01(Mathf.Sin(Mathf.Deg2Rad * elevation));
            currentIntensity = Mathf.SmoothDamp(currentIntensity, targetIntensity * maximumLightIntensity, ref intensityVelocity, 0.3f);
            _light.intensity = currentIntensity;
            _light.shadowStrength = currentIntensity / maximumLightIntensity;

            // Update shader global direction
            if (celestialType == CelestialType.Sun)
                Shader.SetGlobalVector("_SunDirection", -transform.forward);
            else
                Shader.SetGlobalVector("_MoonDirection", -transform.forward);

            UpdateLightState();
        }

        private void UpdateLightState()
        {
            float dot = Vector3.Dot(Vector3.down, transform.forward);
            var newState = dot > 0 ? LightState.Up : LightState.Down;

            if (newState != state)
            {
                state = newState;
                if (state == LightState.Up) OnRise?.Invoke();
                else OnSet?.Invoke();
            }
        }
    }
}
