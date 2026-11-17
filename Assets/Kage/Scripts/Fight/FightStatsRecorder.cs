using System;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// Builds the <see cref="FightResult"/> (the rating input of a fight) from the event stream and the player's public state
    /// (ADR-0003, SAN-73). It only listens: nothing else keeps its own combo or parry count.
    /// </summary>
    public sealed class FightStatsRecorder : IDisposable
    {
        /// <summary>JS comboT: a combo lasts 1.1 s after the last hit (66 frames).</summary>
        public const int ComboGapFrames = 66;

        /// <summary>Damage below this is "no damage" for the flawless seal: 1 JS health point at the 100 / 232 health scale.</summary>
        public const float FlawlessDamage = 100f / 232f;

        readonly CombatEvents _events;
        readonly Fighter _player;
        int _startFrame, _lastFrame, _koFrame = -1, _combo, _lastHitFrame = int.MinValue;
        bool _won;
        float _damageTaken;

        public FightStatsRecorder(CombatEvents events, Fighter player)
        {
            _events = events;
            _player = player;
            _events.Published += OnEvent;
        }

        public int MaxCombo { get; private set; }
        public int Parries { get; private set; }

        /// <summary>The fight clock frame the fight proper began on (after the intro).</summary>
        public void MarkFightStart(int frame) { _startFrame = frame; _lastFrame = frame; }

        void OnEvent(CombatEvent e)
        {
            if (e.Frame > _lastFrame) _lastFrame = e.Frame;
            switch (e.Type)
            {
                case CombatEventType.Hit:
                case CombatEventType.Blocked:
                case CombatEventType.Countered:
                    if (e.Target == _player)
                    {
                        _damageTaken += e.Damage;
                        _combo = 0;
                    }
                    else if (e.Type == CombatEventType.Hit && e.Source == _player && e.Target != null)
                    {
                        if (e.Frame - _lastHitFrame > ComboGapFrames) _combo = 0;
                        _lastHitFrame = e.Frame;
                        if (++_combo > MaxCombo) MaxCombo = _combo;
                    }
                    break;
                case CombatEventType.Parried:
                    if (e.Target == _player) Parries++;
                    break;
                case CombatEventType.KnockedOut:
                    if (_koFrame < 0)
                    {
                        _koFrame = e.Frame;
                        _won = e.Target != _player;
                    }
                    break;
            }
        }

        /// <summary>The result as of now: the knockout ends the fight; before one, it is a loss measured to the last event.</summary>
        public FightResult Result()
        {
            int end = _koFrame >= 0 ? _koFrame : _lastFrame;
            return new FightResult
            {
                win = _won,
                hpLeft = Math.Max(0.0, Math.Min(1.0, _player.health / Math.Max(1f, _player.maxHealth))),
                time = (end - _startFrame) / 60.0,
                maxCombo = MaxCombo,
                parries = Parries,
                flawless = _won && _damageTaken < FlawlessDamage,
            };
        }

        public void Dispose() { _events.Published -= OnEvent; }
    }
}
