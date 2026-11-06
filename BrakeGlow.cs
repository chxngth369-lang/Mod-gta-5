using System;
using GTA;
using GTA.Math;
using GTA.Native;

// BrakeGlow: hard braking heats each wheel's brake; hot brakes glow red-orange.
// Heat is simulated per wheel (front brakes take more load), then cools with time and speed.
// The glow is a small point light at each wheel hub (not an emissive brake disc).
public class BrakeGlow : Script
{
    static readonly string[] WheelBones = { "wheel_lf", "wheel_rf", "wheel_lr", "wheel_rr" };
    static readonly float[] BrakeShare = { 1.3f, 1.3f, 0.7f, 0.7f }; // front 65%, rear 35%

    // Tuning
    const float HeatGain = 0.0008f;     // heat per (deceleration * speed) per second
    const float BaseCooling = 0.12f;    // cooling per second when parked
    const float AirCooling = 0.012f;    // extra cooling per second per m/s of speed
    const float GlowStart = 0.25f;      // heat below this: no glow
    const float MinSpeed = 2.0f;        // m/s, below this braking adds no heat
    const float MaxDecel = 25.0f;       // m/s^2, ignores crash spikes above this

    readonly float[] heat = new float[4];
    float lastSpeed;
    int lastHandle;

    public BrakeGlow()
    {
        Tick += OnTick;
    }

    void Reset()
    {
        for (int i = 0; i < heat.Length; i++) heat[i] = 0f;
        lastHandle = 0;
        lastSpeed = 0f;
    }

    void OnTick(object sender, EventArgs e)
    {
        Ped player = Game.Player.Character;
        Vehicle veh = player.CurrentVehicle;

        if (veh == null || !veh.Exists() || player.SeatIndex != VehicleSeat.Driver)
        {
            if (lastHandle != 0) Reset();
            return;
        }

        if (veh.Handle != lastHandle)
        {
            Reset();
            lastHandle = veh.Handle;
            lastSpeed = veh.Speed;
        }

        float dt = Game.LastFrameTime;
        if (dt <= 0f) return;

        float speed = veh.Speed;
        float decel = (lastSpeed - speed) / dt;
        lastSpeed = speed;

        bool braking = Game.IsControlPressed(Control.VehicleBrake) || decel > 3f;
        if (decel < 0f) decel = 0f;
        if (decel > MaxDecel) decel = MaxDecel;

        float cooling = BaseCooling + speed * AirCooling;
        bool anyGlow = false;

        for (int i = 0; i < heat.Length; i++)
        {
            if (braking && speed > MinSpeed)
                heat[i] += decel * speed * HeatGain * BrakeShare[i] * dt;

            heat[i] -= heat[i] * cooling * dt;

            if (heat[i] > 1f) heat[i] = 1f;
            if (heat[i] < 0f) heat[i] = 0f;
            if (heat[i] > GlowStart) anyGlow = true;
        }

        if (!anyGlow) return;

        for (int i = 0; i < heat.Length; i++)
        {
            if (heat[i] <= GlowStart) continue;

            EntityBone bone = veh.Bones[WheelBones[i]];
            if (!bone.IsValid) continue;

            float glow = (heat[i] - GlowStart) / (1f - GlowStart); // 0..1
            int r = 255;
            int g = (int)(40f + 110f * glow);
            int b = (int)(10f + 20f * glow);
            float range = 0.5f + 1.0f * glow;
            float intensity = 2f + 6f * glow;

            Vector3 pos = bone.Position;
            Function.Call(Hash.DRAW_LIGHT_WITH_RANGE,
                pos.X, pos.Y, pos.Z, r, g, b, range, intensity);
        }
    }
}
