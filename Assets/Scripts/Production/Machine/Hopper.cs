using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// The Portionierer's hopper (Trichter). Implements IFillLevelSource so the LevelSensor on the
    /// same GameObject can report the normalized level.
    ///
    /// Content model: the dough balls (DoughSphere, MixedDough ProductToken) that fall into the
    /// hopper stay physical objects. The intake trigger on this GameObject registers them in
    /// arrival order. <see cref="ConsumeDough"/> draws from the oldest ball first: its
    /// MeasuredWeightGrams is reduced and its scale shrinks with the remaining mass; at 0 g it is
    /// destroyed and the next ball is used. A floor collider (gate) at the outlet keeps shrunken
    /// balls from falling through.
    ///
    /// <see cref="TryDrain"/> empties the hopper without creating products: the holding colliders
    /// are switched off until the balls have dropped out below the hopper, then switched on again.
    /// </summary>
    public class Hopper : MonoBehaviour, IFillLevelSource
    {
        [SerializeField] private float _capacityGrams = 300000f; // 300kg

        [Tooltip("Only products in this state are accepted by the intake trigger.")]
        [SerializeField] private ProductState _acceptedState = ProductState.MixedDough;

        [Header("Drain")]
        [Tooltip("Colliders that hold the content (walls, funnel, outlet gate). Disabled while draining.")]
        [SerializeField] private Collider[] _drainColliders = Array.Empty<Collider>();

        [Tooltip("Safety limit: colliders are re-enabled after this time even if content is still inside.")]
        [SerializeField] private float _drainMaxSeconds = 3f;

        private readonly List<DoughBall> _balls = new List<DoughBall>();
        private float _looseGrams;

        /// <summary>Raised after a drain has finished and the colliders are active again.</summary>
        public event Action DrainFinished;

        public bool IsDraining { get; private set; }

        public float CurrentAmountGrams
        {
            get
            {
                float total = _looseGrams;
                foreach (DoughBall ball in _balls)
                {
                    total += ball.RemainingGrams;
                }
                return total;
            }
        }

        public bool HasDough => CurrentAmountGrams > 0f;

        /// <summary>0..1, consumed by LevelSensor via IFillLevelSource.</summary>
        public float NormalizedLevel => _capacityGrams > 0f
            ? Mathf.Clamp01(CurrentAmountGrams / _capacityGrams)
            : 0f;

        /// <summary>
        /// Removes <paramref name="amountGrams"/> from the content, oldest ball first. Shrinks the
        /// balls accordingly and destroys every ball that reaches 0 g. Returns the amount actually
        /// removed (less than requested if the hopper runs empty).
        /// </summary>
        public float ConsumeDough(float amountGrams)
        {
            float remaining = Mathf.Max(0f, amountGrams);

            float fromLoose = Mathf.Min(_looseGrams, remaining);
            _looseGrams -= fromLoose;
            remaining -= fromLoose;

            while (remaining > 0f && _balls.Count > 0)
            {
                DoughBall ball = _balls[0];
                if (!ball.IsAlive)
                {
                    _balls.RemoveAt(0);
                    continue;
                }

                float taken = Mathf.Min(ball.RemainingGrams, remaining);
                ball.RemainingGrams -= taken;
                remaining -= taken;

                if (ball.RemainingGrams <= 0f)
                {
                    _balls.RemoveAt(0);
                    ball.Token.Product = null; // clear before the deferred Destroy
                    Destroy(ball.Token.gameObject);
                }
                else
                {
                    ball.ApplyScale();
                }
            }

            return Mathf.Max(0f, amountGrams) - remaining;
        }

        /// <summary>
        /// Lets the whole content drop out of the hopper without creating products. Returns false
        /// if a drain is already running or the hopper is empty.
        /// </summary>
        public bool TryDrain()
        {
            if (IsDraining || !HasDough)
            {
                return false;
            }

            StartCoroutine(DrainRoutine());
            return true;
        }

        private IEnumerator DrainRoutine()
        {
            IsDraining = true;

            List<Transform> drained = new List<Transform>();
            foreach (DoughBall ball in _balls)
            {
                if (ball.IsAlive)
                {
                    drained.Add(ball.Token.transform);
                    if (ball.Body != null)
                    {
                        ball.Body.WakeUp();
                    }
                }
            }
            _balls.Clear();
            _looseGrams = 0f;

            SetDrainCollidersEnabled(false);

            // The hopper model's bottom sits at this GameObject's origin.
            float bottomY = transform.position.y;
            float elapsed = 0f;
            while (elapsed < _drainMaxSeconds && !AllBelow(drained, bottomY))
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetDrainCollidersEnabled(true);
            IsDraining = false;
            DrainFinished?.Invoke();
        }

        private static bool AllBelow(List<Transform> objects, float y)
        {
            foreach (Transform t in objects)
            {
                if (t == null)
                {
                    continue;
                }

                Collider col = t.GetComponentInChildren<Collider>();
                float top = col != null ? col.bounds.max.y : t.position.y;
                if (top > y)
                {
                    return false;
                }
            }
            return true;
        }

        private void SetDrainCollidersEnabled(bool isEnabled)
        {
            foreach (Collider col in _drainColliders)
            {
                if (col != null)
                {
                    col.enabled = isEnabled;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsDraining)
            {
                return;
            }

            ProductToken token = other.GetComponentInParent<ProductToken>();
            if (token == null || token.Product == null || token.Product.CurrentState != _acceptedState || IsTracked(token))
            {
                return;
            }

            if (!token.Product.MeasuredWeightGrams.HasValue)
            {
                Debug.LogWarning($"{name}: product {token.Product.InstanceId} has no MeasuredWeightGrams - ignored.", this);
                return;
            }

            _balls.Add(new DoughBall(token));
        }

        private void OnTriggerExit(Collider other)
        {
            // A ball that leaves the hopper (picked out, bounced out) no longer counts as content.
            ProductToken token = other.GetComponentInParent<ProductToken>();
            if (token != null)
            {
                _balls.RemoveAll(b => b.Token == token);
            }
        }

        private bool IsTracked(ProductToken token)
        {
            foreach (DoughBall ball in _balls)
            {
                if (ball.Token == token)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>One physical dough ball inside the hopper.</summary>
        private sealed class DoughBall
        {
            public readonly ProductToken Token;
            public readonly Rigidbody Body;
            private readonly float _initialGrams;
            private readonly Vector3 _initialScale;

            public DoughBall(ProductToken token)
            {
                Token = token;
                Body = token.GetComponent<Rigidbody>();
                _initialGrams = token.Product.MeasuredWeightGrams ?? 0f;
                _initialScale = token.transform.localScale;
            }

            public bool IsAlive => Token != null && Token.Product != null;

            public float RemainingGrams
            {
                get => IsAlive ? Token.Product.MeasuredWeightGrams ?? 0f : 0f;
                set
                {
                    if (IsAlive)
                    {
                        Token.Product.MeasuredWeightGrams = Mathf.Max(0f, value);
                    }
                }
            }

            /// <summary>Scale follows the remaining volume (mass), so the diameter shrinks with the cube root.</summary>
            public void ApplyScale()
            {
                if (!IsAlive || _initialGrams <= 0f)
                {
                    return;
                }

                float factor = Mathf.Pow(Mathf.Clamp01(RemainingGrams / _initialGrams), 1f / 3f);
                Token.transform.localScale = _initialScale * factor;
                if (Body != null)
                {
                    Body.WakeUp();
                }
            }
        }
    }
}
