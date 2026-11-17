using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    // The physics layers, their collision matrix and the physics materials live in project settings and
    // assets (ADR-0007, SAN-68), not in code. The expected table is docs/physics.md.
    public class PhysicsSettingsTests
    {
        static readonly (string name, int layer)[] Layers =
        {
            ("Fighter", 8), ("Ragdoll", 9), ("Ground", 10), ("Prop", 11), ("Hurtbox", 12),
            ("Push", 13), ("PushPassThrough", 14), ("Projectile", 15), ("Hazard", 16),
        };

        // who each game layer collides with; every other pair of game layers ignores each other
        static readonly Dictionary<int, int[]> CollidesWith = new()
        {
            [8] = new[] { 10 },
            [9] = new[] { 10 },
            [10] = new[] { 8, 9, 11, 15 },
            [11] = new[] { 10 },
            [12] = new int[0],
            [13] = new[] { 13 },
            [14] = new int[0],
            [15] = new[] { 10 },
            [16] = new int[0],
        };

        [Test]
        public void The_game_layers_are_named_in_the_tag_manager()
        {
            foreach (var (name, layer) in Layers)
                Assert.AreEqual(layer, LayerMask.NameToLayer(name), name);
            Assert.AreEqual(PhysicsLayers.Push, LayerMask.NameToLayer("Push"));
            Assert.AreEqual(PhysicsLayers.PushPassThrough, LayerMask.NameToLayer("PushPassThrough"));
        }

        [Test]
        public void The_layer_collision_matrix_in_project_settings_is_the_documented_one()
        {
            foreach (var (a, la) in Layers)
            foreach (var (b, lb) in Layers)
            {
                bool expected = System.Array.IndexOf(CollidesWith[la], lb) >= 0;
                Assert.AreEqual(expected, !Physics2D.GetIgnoreLayerCollision(la, lb), $"{a} x {b}");
            }
        }

        [Test]
        public void Builtin_layers_touch_only_the_ground_among_the_game_layers()
        {
            foreach (var (name, layer) in Layers)
                Assert.AreEqual(layer == PhysicsLayers.Ground, !Physics2D.GetIgnoreLayerCollision(0, layer), $"Default x {name}");
        }

        [Test]
        public void The_physics_materials_are_assets()
        {
            var none = PhysicsMaterials.NoFriction;
            Assert.IsNotNull(none);
            Assert.AreEqual(0f, none.friction);
            Assert.AreEqual(0f, none.bounciness);
            var rag = PhysicsMaterials.Ragdoll;
            Assert.IsNotNull(rag);
            Assert.AreEqual(0.7f, rag.friction, 1e-6f);
            Assert.AreEqual(0.12f, rag.bounciness, 1e-6f);
            Assert.AreSame(none, Resources.Load<PhysicsMaterial2D>("Physics/NoFriction"), "loaded from the asset, not created in code");
        }
    }
}
