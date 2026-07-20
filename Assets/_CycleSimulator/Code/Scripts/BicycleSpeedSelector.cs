using UnityEngine;

namespace SBPScripts
{
    /// <summary>
    /// Adds a five-option speed dropdown, optional acceleration assistance,
    /// and adjustable gravity to BicycleController.
    /// Attach this component to the same GameObject as BicycleController.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BicycleController))]
    [DefaultExecutionOrder(-50)]
    public sealed class BicycleSpeedSelector : MonoBehaviour
    {
        public enum SpeedOption
        {
            VerySlow,
            Slow,
            Normal,
            Fast,
            VeryFast
        }

        [Tooltip("Select the bicycle speed preset. Changes are blended smoothly during play mode.")]
        public SpeedOption speed = SpeedOption.Normal;

        [Min(0f)]
        [Tooltip(
            "Extra forward acceleration used to reach the selected top speed faster. " +
            "This uses ForceMode.Acceleration. Set to 0 to disable assistance.")]
        public float topSpeedAcceleration = 35f;

        [Min(0f)]
        [Tooltip(
            "Gravity strength applied to the bicycle. " +
            "0 disables gravity, 1 uses normal gravity, and 2 uses double gravity.")]
        public float gravityModifier = 2f;

        // Speed preset transitions remain smooth.
        private const float SmoothTime = 0.75f;

        // Acceleration assistance smoothly fades out near the top speed.
        private const float AssistCutoffRatio = 0.98f;
        private const float AssistSmoothTime = 0.15f;

        private BicycleController bicycleController;
        private SpeedOption appliedSpeed;

        private float baseTopSpeed;
        private float baseTorque;
        private float basePedalingSpeed;

        private float targetTopSpeed;
        private float targetTorque;
        private float targetPedalingSpeed;

        private float topSpeedVelocity;
        private float torqueVelocity;
        private float pedalingSpeedVelocity;

        private float currentAccelerationAssist;
        private float accelerationAssistVelocity;

        private void Awake()
        {
            bicycleController = GetComponent<BicycleController>();

            baseTopSpeed = bicycleController.topSpeed;
            baseTorque = bicycleController.torque;

            basePedalingSpeed = bicycleController.pedalAdjustments != null
                ? bicycleController.pedalAdjustments.pedalingSpeed
                : 0f;

            appliedSpeed = speed;
            UpdateTargets();
        }

        private void FixedUpdate()
        {
            if (speed != appliedSpeed)
            {
                appliedSpeed = speed;
                UpdateTargets();
            }

            SmoothPresetValues();
            ApplyTopSpeedAcceleration();
            ApplyGravityModifier();
        }

        private void SmoothPresetValues()
        {
            bicycleController.topSpeed = Mathf.SmoothDamp(
                bicycleController.topSpeed,
                targetTopSpeed,
                ref topSpeedVelocity,
                SmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);

            bicycleController.torque = Mathf.SmoothDamp(
                bicycleController.torque,
                targetTorque,
                ref torqueVelocity,
                SmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);

            if (bicycleController.pedalAdjustments != null)
            {
                bicycleController.pedalAdjustments.pedalingSpeed =
                    Mathf.SmoothDamp(
                        bicycleController.pedalAdjustments.pedalingSpeed,
                        targetPedalingSpeed,
                        ref pedalingSpeedVelocity,
                        SmoothTime,
                        Mathf.Infinity,
                        Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Adds extra forward acceleration while the player is accelerating.
        /// Assistance becomes weaker as the bicycle approaches top speed.
        /// </summary>
        private void ApplyTopSpeedAcceleration()
        {
            Rigidbody bicycleRigidbody = bicycleController.rb;

            if (bicycleRigidbody == null || topSpeedAcceleration <= 0f)
            {
                ResetAccelerationAssist();
                return;
            }

            float throttle = Mathf.Clamp01(
                bicycleController.rawCustomAccelerationAxis);

            float currentTopSpeed = Mathf.Max(
                0.01f,
                bicycleController.topSpeed);

            // Use forward speed so sideways or vertical motion does not
            // incorrectly disable acceleration assistance.
            float forwardSpeed = Mathf.Max(
                0f,
                Vector3.Dot(
                    bicycleRigidbody.velocity,
                    transform.forward));

            float normalizedSpeed = Mathf.Clamp01(
                forwardSpeed /
                (currentTopSpeed * AssistCutoffRatio));

            float remainingSpeedFactor =
                1f - Mathf.SmoothStep(0f, 1f, normalizedSpeed);

            float desiredAcceleration =
                topSpeedAcceleration *
                throttle *
                remainingSpeedFactor;

            currentAccelerationAssist = Mathf.SmoothDamp(
                currentAccelerationAssist,
                desiredAcceleration,
                ref accelerationAssistVelocity,
                AssistSmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);

            if (currentAccelerationAssist > 0.001f)
            {
                bicycleRigidbody.AddForce(
                    transform.forward * currentAccelerationAssist,
                    ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Modifies Unity's normal gravity without changing Rigidbody mass.
        /// A value of 1 applies normal gravity, 0 cancels gravity,
        /// and values above 1 increase gravity.
        /// </summary>
        private void ApplyGravityModifier()
        {
            Rigidbody bicycleRigidbody = bicycleController.rb;

            if (bicycleRigidbody == null)
            {
                return;
            }

            float safeGravityModifier = Mathf.Max(0f, gravityModifier);

            if (bicycleRigidbody.useGravity)
            {
                // Unity already applies normal gravity. Add only the
                // difference required by the selected modifier.
                Vector3 additionalGravity =
                    Physics.gravity * (safeGravityModifier - 1f);

                bicycleRigidbody.AddForce(
                    additionalGravity,
                    ForceMode.Acceleration);
            }
            else if (safeGravityModifier > 0f)
            {
                // Apply custom gravity even when Rigidbody gravity is disabled.
                bicycleRigidbody.AddForce(
                    Physics.gravity * safeGravityModifier,
                    ForceMode.Acceleration);
            }
        }

        private void ResetAccelerationAssist()
        {
            currentAccelerationAssist = Mathf.SmoothDamp(
                currentAccelerationAssist,
                0f,
                ref accelerationAssistVelocity,
                AssistSmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime);
        }

        /// <summary>
        /// Allows another script or UI control to change the preset at runtime.
        /// </summary>
        public void SetSpeed(SpeedOption newSpeed)
        {
            speed = newSpeed;
        }

        /// <summary>
        /// Allows another script or UI control to change gravity at runtime.
        /// </summary>
        public void SetGravityModifier(float newGravityModifier)
        {
            gravityModifier = Mathf.Max(0f, newGravityModifier);
        }

        private void UpdateTargets()
        {
            float speedMultiplier;
            float torqueMultiplier;
            float pedalingMultiplier;

            switch (speed)
            {
                case SpeedOption.VerySlow:
                    speedMultiplier = 0.80f;
                    torqueMultiplier = 1.1f;
                    pedalingMultiplier = 1.20f;
                    break;

                case SpeedOption.Slow:
                    speedMultiplier = 2.10f;
                    torqueMultiplier = 2.34f;
                    pedalingMultiplier = 2.40f;
                    break;

                case SpeedOption.Fast:
                    speedMultiplier = 6.50f;
                    torqueMultiplier = 6.0f;
                    pedalingMultiplier = 5.90f;
                    break;

                case SpeedOption.VeryFast:
                    speedMultiplier = 12f;
                    torqueMultiplier = 7f;
                    pedalingMultiplier = 4.5f;
                    break;

                default:
                    speedMultiplier = 1f;
                    torqueMultiplier = 1f;
                    pedalingMultiplier = 1f;
                    break;
            }

            targetTopSpeed = Mathf.Max(
                0f,
                baseTopSpeed * speedMultiplier);

            targetTorque = Mathf.Max(
                0f,
                baseTorque * torqueMultiplier);

            targetPedalingSpeed = Mathf.Max(
                0f,
                basePedalingSpeed * pedalingMultiplier);
        }
    }
}