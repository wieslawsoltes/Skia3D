using System.Numerics;
using Skia3D.Animation;
using Skia3D.Scene;
using Xunit;

namespace Skia3D.Core.Tests;

public sealed class AnimationStateMachineTests
{
    [Fact]
    public void AnimationStateMachine_TriggerSwitchesStates()
    {
        var scene = new Scene();
        var node = new SceneNode("Root");
        scene.AddRoot(node);

        var clipA = new AnimationClip("A");
        var trackA = new TransformTrack("Root");
        trackA.TranslationKeys.Add(new Keyframe<Vector3>(0f, Vector3.Zero));
        clipA.Tracks.Add(trackA);
        clipA.RecalculateDuration();

        var clipB = new AnimationClip("B");
        var trackB = new TransformTrack("Root");
        trackB.TranslationKeys.Add(new Keyframe<Vector3>(0f, new Vector3(5f, 0f, 0f)));
        clipB.Tracks.Add(trackB);
        clipB.RecalculateDuration();

        var playerA = new AnimationPlayer(clipA.Bind(scene)) { Loop = false };
        var playerB = new AnimationPlayer(clipB.Bind(scene)) { Loop = false };

        var stateA = new AnimationState("Idle") { Player = playerA };
        var stateB = new AnimationState("Move") { Player = playerB };

        var machine = new AnimationStateMachine();
        machine.AddState(stateA, isDefault: true);
        machine.AddState(stateB);
        var transition = machine.AddTransition(stateA, stateB);
        transition.Duration = 0f;
        transition.Conditions.Add(AnimationCondition.Trigger("go"));

        machine.Update(0.016f);
        Assert.Equal(0f, node.Transform.LocalPosition.X, 3);

        machine.Parameters.SetTrigger("go");
        machine.Update(0.016f);

        Assert.InRange(node.Transform.LocalPosition.X, 4.9f, 5.1f);
    }
}
