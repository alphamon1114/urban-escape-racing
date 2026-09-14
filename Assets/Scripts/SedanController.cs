using UnityEngine;

namespace UrbanEscape
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SedanController : MonoBehaviour
    {
        public WheelCollider[] wheels;
        public Transform[] wheelMeshes;
        public float driveTorque = 620f;
        public float brakeTorque = 2600f;
        public float steeringRate = 85f;
        public float countersteerRate = 190f;
        public float countersteerAssist = 2.8f;
        const float RearGrip = 1.35f;
        public float SpeedKph => body.linearVelocity.magnitude * 3.6f;
        public float SteeringAngle => steering;
        Rigidbody body;
        float steering;
        float handbrakeBlend;
        float throttle, steerInput;
        bool braking, handbrake, resetRequested;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = 1550f;
            body.centerOfMass = new Vector3(0, -0.25f, 0.1f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].ConfigureVehicleSubsteps(5f, 12, 15);
                var friction = wheels[i].sidewaysFriction;
                // Front tires reach their limit first; the rear stays planted until handbraking.
                friction.extremumSlip = i < 2 ? 0.22f : 0.30f;
                friction.extremumValue = 1f;
                friction.asymptoteSlip = 0.65f;
                friction.asymptoteValue = i < 2 ? 0.78f : 0.95f;
                wheels[i].sidewaysFriction = friction;
            }
        }

        void Update()
        {
            throttle = Input.GetKey(KeyCode.W) ? 1f : 0f;
            braking = Input.GetKey(KeyCode.S);
            steerInput = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            handbrake = Input.GetKey(KeyCode.Space);
            resetRequested |= Input.GetKeyDown(KeyCode.R);
        }

        void FixedUpdate()
        {
            if (resetRequested || transform.position.y < -10f)
            {
                ResetVehicle();
                resetRequested = false;
            }
            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float speedBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 140f, SpeedKph));
            float steeringLimit = Mathf.Lerp(32f, 7f, speedBlend);
            // Restore lock only while correcting an actual slide, not on every direction change.
            float lateralSpeed = Vector3.Dot(body.linearVelocity, transform.right);
            float slipAngle = Mathf.Atan2(lateralSpeed, Mathf.Max(1f, Mathf.Abs(forwardSpeed))) * Mathf.Rad2Deg;
            float yaw = Vector3.Dot(body.angularVelocity, transform.up);
            bool correctingSlide = forwardSpeed > 5f && steerInput * slipAngle > 0f && steerInput * yaw < 0f;
            if (correctingSlide)
                steeringLimit = Mathf.Lerp(steeringLimit, 32f, Mathf.InverseLerp(3f, 18f, Mathf.Abs(slipAngle)));
            bool reversingSteering = steering * steerInput < 0f;
            float rate = reversingSteering ? Mathf.Max(steeringRate, countersteerRate) : steeringRate * Mathf.Lerp(1f, 0.35f, speedBlend);
            steering = Mathf.MoveTowards(steering, steerInput * steeringLimit, rate * Time.fixedDeltaTime);
            handbrakeBlend = Mathf.MoveTowards(handbrakeBlend, handbrake ? 1f : 0f,
                Time.fixedDeltaTime / (handbrake ? 0.12f : 0.45f));
            float motor = 0f;
            float brake = 0f;
            if (braking)
            {
                if (forwardSpeed > 0.35f) brake = brakeTorque;
                else motor = -driveTorque * 0.55f * Mathf.Clamp01((32f + forwardSpeed * 3.6f) / 8f);
            }
            else if (throttle > 0f)
            {
                if (forwardSpeed < -0.35f) brake = brakeTorque;
                else motor = driveTorque * Mathf.Clamp01((175f - SpeedKph) / 45f);
            }
            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                wheel.steerAngle = i < 2 ? steering : 0f;
                wheel.motorTorque = handbrake && i >= 2 ? 0f : motor;
                wheel.brakeTorque = i >= 2 && handbrake ? 4200f : brake;
                var friction = wheel.sidewaysFriction;
                // Preserve momentum through an over-fast turn; Space deliberately releases the rear.
                friction.stiffness = i < 2 ? 1.05f : Mathf.Lerp(RearGrip, 0.65f, handbrakeBlend);
                wheel.sidewaysFriction = friction;
            }
            ApplyCountersteerAssist(forwardSpeed);
            ApplyHandbrakeSteering(forwardSpeed);
            // Aerodynamic drag; lateral velocity is never snapped to the car heading.
            body.AddForce(-body.linearVelocity * (18f + 0.38f * body.linearVelocity.magnitude));
        }

        void ApplyHandbrakeSteering(float forwardSpeed)
        {
            if (!handbrake || forwardSpeed < 5f || Mathf.Abs(steerInput) < 0.1f) return;
            int grounded = 0;
            foreach (var wheel in wheels) if (wheel.isGrounded) grounded++;
            if (grounded < 3 || Vector3.Dot(transform.up, Vector3.up) < 0.65f) return;
            // Bounded arcade yaw assistance keeps steering useful with the rear wheels locked.
            // It also follows countersteering; never forces lateral velocity or heading.
            float yaw = Vector3.Dot(body.angularVelocity, transform.up);
            float targetYaw = (steering / 32f) * 0.85f;
            float acceleration = Mathf.Clamp((targetYaw - yaw) * 2f, -1.2f, 1.2f);
            body.AddTorque(transform.up * acceleration * handbrakeBlend, ForceMode.Acceleration);
        }

        void ApplyCountersteerAssist(float forwardSpeed)
        {
            // Help only an intentional correction of an existing slide, on the ground.
            // Keep a lighter correction available during handbrake drifting.
            if (forwardSpeed < 5f || Mathf.Abs(steerInput) < 0.1f) return;
            int grounded = 0;
            foreach (var wheel in wheels) if (wheel.isGrounded) grounded++;
            if (grounded < 3 || Vector3.Dot(transform.up, Vector3.up) < 0.65f) return;
            float lateralSpeed = Vector3.Dot(body.linearVelocity, transform.right);
            float slipAngle = Mathf.Atan2(lateralSpeed, forwardSpeed) * Mathf.Rad2Deg;
            float yaw = Vector3.Dot(body.angularVelocity, transform.up);
            if (steerInput * slipAngle <= 0f || steerInput * yaw >= 0f) return;
            float slide = Mathf.InverseLerp(3f, 16f, Mathf.Abs(slipAngle));
            float damping = Mathf.Min(Mathf.Max(0f, countersteerAssist) * slide, 0.5f / Time.fixedDeltaTime);
            if (handbrake) damping *= 0.5f;
            body.AddTorque(transform.up * (-yaw * damping), ForceMode.Acceleration);
        }

        void LateUpdate()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].GetWorldPose(out var position, out var rotation);
                wheelMeshes[i].SetPositionAndRotation(position, rotation);
            }
        }

        public void ResetVehicle()
        {
            body.position = new Vector3(0f, 1f, -80f);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            steering = 0f;
            handbrakeBlend = 0f;
            foreach (var wheel in wheels) { wheel.motorTorque = 0f; wheel.brakeTorque = 0f; wheel.steerAngle = 0f; }
        }
    }
}
