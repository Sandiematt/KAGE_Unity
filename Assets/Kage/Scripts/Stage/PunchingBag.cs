using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Training dummy from the reference's opening: a heavy bag on a rope of HingeJoint2D links
    /// under a wooden frame. Strikes apply impulses at the contact point, so it swings and spins
    /// with real 2D physics.
    /// </summary>
    public class PunchingBag : MonoBehaviour, IHittable
    {
        Rigidbody2D _bag;
        Rigidbody2D[] _links;
        Ribbon _rope;
        Vector2 _anchor;
        MeshRenderer[] _bagRenderers;
        float _flash;
        MaterialPropertyBlock _mpb;
        public float totalDamage;
        const float BagLen = 1.15f;

        public static PunchingBag Build(Transform parent, Vector2 anchor)
        {
            var go = new GameObject("TrainingBag");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<PunchingBag>();
            b.Init(anchor);
            return b;
        }

        void Init(Vector2 anchor)
        {
            _anchor = anchor;
            _mpb = new MaterialPropertyBlock();
            Color wood = KMath.Hex("#0C0608");
            // frame: two posts and a beam (behind the fighters)
            var frame = new GameObject("Frame").transform;
            frame.SetParent(transform, false);
            MeshKit.MakeRenderer("PostL", frame, MeshKit.Poly(new[] { new Vector2(anchor.x - 1.0f, 0f), new Vector2(anchor.x - 0.88f, 0f), new Vector2(anchor.x - 0.9f, anchor.y + 0.12f), new Vector2(anchor.x - 0.98f, anchor.y + 0.12f) }, wood), MeshKit.Alpha, -1000);
            MeshKit.MakeRenderer("PostR", frame, MeshKit.Poly(new[] { new Vector2(anchor.x + 0.88f, 0f), new Vector2(anchor.x + 1.0f, 0f), new Vector2(anchor.x + 0.98f, anchor.y + 0.12f), new Vector2(anchor.x + 0.9f, anchor.y + 0.12f) }, wood), MeshKit.Alpha, -1000);
            MeshKit.MakeRenderer("Beam", frame, MeshKit.Poly(new[] { new Vector2(anchor.x - 1.2f, anchor.y + 0.04f), new Vector2(anchor.x + 1.2f, anchor.y + 0.04f), new Vector2(anchor.x + 1.15f, anchor.y + 0.18f), new Vector2(anchor.x - 1.15f, anchor.y + 0.18f) }, wood), MeshKit.Alpha, -999);

            var anchorBody = new GameObject("Anchor").AddComponent<Rigidbody2D>();
            anchorBody.transform.SetParent(transform, false);
            anchorBody.transform.position = anchor;
            anchorBody.bodyType = RigidbodyType2D.Static;

            // rope: three short links
            _links = new Rigidbody2D[3];
            Rigidbody2D prev = anchorBody;
            float linkLen = 0.15f;
            for (int i = 0; i < _links.Length; i++)
            {
                var l = new GameObject("RopeLink" + i);
                l.transform.SetParent(transform, false);
                l.layer = KageGame.LayerProp;
                l.transform.position = anchor - new Vector2(0f, linkLen * (i + 1));
                var rb = l.AddComponent<Rigidbody2D>();
                rb.mass = 0.15f;
                rb.linearDamping = 0.4f;
                rb.angularDamping = 0.8f;
                var j = l.AddComponent<HingeJoint2D>();
                j.connectedBody = prev;
                j.autoConfigureConnectedAnchor = false;
                j.anchor = new Vector2(0f, linkLen * 0.5f);
                j.connectedAnchor = i == 0 ? Vector2.zero : new Vector2(0f, -linkLen * 0.5f);
                _links[i] = rb;
                prev = rb;
            }

            // bag
            var bag = new GameObject("Bag");
            bag.transform.SetParent(transform, false);
            bag.layer = KageGame.LayerProp;
            Vector2 top = anchor - new Vector2(0f, linkLen * _links.Length + 0.08f);
            bag.transform.position = top;
            _bag = bag.AddComponent<Rigidbody2D>();
            _bag.mass = 3.2f;
            _bag.linearDamping = 0.35f;
            _bag.angularDamping = 1.2f;
            _bag.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = bag.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.44f, BagLen);
            col.offset = new Vector2(0f, -BagLen * 0.5f);
            var hj = bag.AddComponent<HingeJoint2D>();
            hj.connectedBody = prev;
            hj.autoConfigureConnectedAnchor = false;
            hj.anchor = new Vector2(0f, 0.08f);
            hj.connectedAnchor = new Vector2(0f, -linkLen * 0.5f);

            // visuals (bag-local, pivot at the top)
            Color leather = KMath.Hex("#1A0F11"), band = KMath.Hex("#B8323A");
            var shape = new Vector2[]
            {
                new(-0.17f, 0f), new(0.17f, 0f), new(0.22f, -0.12f), new(0.23f, -0.6f), new(0.22f, -1.02f), new(0.15f, -1.13f),
                new(-0.15f, -1.13f), new(-0.22f, -1.02f), new(-0.23f, -0.6f), new(-0.22f, -0.12f),
            };
            var body = MeshKit.MakeRenderer("BagBody", bag.transform, MeshKit.Poly(shape, leather), MeshKit.Alpha, -400);
            var rim = MeshKit.MakeRenderer("BagRim", bag.transform, MeshKit.Poly(MeshKit.Grow(shape, 0.012f), Palette.Rim), MeshKit.Alpha, -401);
            rim.transform.localPosition = new Vector3(0.008f, 0.006f, 0f);
            var b1 = MeshKit.MakeRenderer("Band", bag.transform, MeshKit.Poly(new[] { new Vector2(-0.225f, -0.2f), new Vector2(0.225f, -0.2f), new Vector2(0.23f, -0.28f), new Vector2(-0.23f, -0.28f) }, band), MeshKit.Alpha, -399);
            var b2 = MeshKit.MakeRenderer("Band", bag.transform, MeshKit.Poly(new[] { new Vector2(-0.23f, -0.86f), new Vector2(0.23f, -0.86f), new Vector2(0.225f, -0.94f), new Vector2(-0.225f, -0.94f) }, band), MeshKit.Alpha, -399);
            var kanji = MeshKit.MakeRenderer("Mark", bag.transform, MeshKit.Ring(new Vector2(0f, -0.57f), 0.11f, 0.085f, KMath.Hex("#5E1418"), 28), MeshKit.Alpha, -398);
            _bagRenderers = new[] { body, b1, b2, kanji };

            _rope = new Ribbon("Rope", transform, MeshKit.Alpha, -402);
        }

        void LateUpdate()
        {
            if (_bag == null) return;
            _rope.Begin();
            Vector2 prev = _anchor;
            Color rc = KMath.Hex("#2A1A16");
            void Seg(Vector2 a, Vector2 b)
            {
                Vector2 d = (b - a).normalized; Vector2 n = new(-d.y, d.x);
                _rope.Break();
                _rope.Add(a + n * 0.018f, a - n * 0.018f, rc, rc);
                _rope.Add(b + n * 0.018f, b - n * 0.018f, rc, rc);
            }
            foreach (var l in _links) { Seg(prev, l.position); prev = l.position; }
            Seg(prev, _bag.transform.TransformPoint(new Vector3(0f, 0.08f, 0f)));
            _rope.End();

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime * 10f);
                _mpb.SetColor("_FlashColor", Color.white);
                _mpb.SetFloat("_Flash", _flash);
                foreach (var r in _bagRenderers) r.SetPropertyBlock(_mpb);
            }
        }

        public void ResetBag()
        {
            totalDamage = 0f;
            float linkLen = 0.15f;
            for (int i = 0; i < _links.Length; i++)
            {
                _links[i].position = _anchor - new Vector2(0f, linkLen * (i + 1));
                _links[i].rotation = 0f;
                _links[i].linearVelocity = Vector2.zero;
                _links[i].angularVelocity = 0f;
            }
            _bag.position = _anchor - new Vector2(0f, linkLen * _links.Length + 0.08f);
            _bag.rotation = 0f;
            _bag.linearVelocity = Vector2.zero;
            _bag.angularVelocity = 0f;
        }

        public bool Overlap(Vector2 p, float r, HitWindow w, out HitZone zone, out Vector2 contact)
        {
            zone = HitZone.Body;
            contact = p;
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = _bag.transform.TransformPoint(new Vector3(0f, -0.14f - i * 0.29f, 0f));
                float rr = r + 0.22f;
                if ((c - p).sqrMagnitude <= rr * rr)
                {
                    contact = Vector2.Lerp(p, c, r / rr);
                    zone = i == 0 ? HitZone.Head : (i == 3 ? HitZone.Legs : HitZone.Body);
                    return true;
                }
            }
            return false;
        }

        public HitResult ReceiveHit(HitInfo info)
        {
            Vector2 j = new Vector2(info.kb.x * 1.5f, Mathf.Clamp(info.kb.y, -1f, 2.5f) * 0.6f) * (info.w.heavy ? 1.5f : 1f);
            _bag.AddForceAtPosition(j * _bag.mass * 0.9f, info.contact, ForceMode2D.Impulse);
            _flash = 0.8f;
            totalDamage += info.dmg;
            return HitResult.Hit;
        }
    }
}
