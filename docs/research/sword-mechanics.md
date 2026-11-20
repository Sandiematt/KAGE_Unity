# Sword mechanics: research note for SAN-118

How real two-handed Japanese swordwork moves (kenjutsu, iaijutsu, kendo), summarised, and what each principle changes in
Kage's katana moves. Written for the SAN-118 animation pass; the owner's review of SAN-74 asked for the sword fight to
follow how sword fights really work. Summaries only; read the sources for the full teaching.

Rig terms used below: the **far hand** (`HandF`) is the lead hand at the guard (the swordsman's right hand), the **near
hand** (`HandN`, gripping with `Pose.Grip`) is the rear hand at the pommel (the left hand). Kage faces right; blade angles
are root space, 0 = forward, 90 = up.

## Sources

- Kamae overview: [Kamae (Wikipedia)](https://en.wikipedia.org/wiki/Kamae),
  [Chūdan-no-kamae](https://en.wikipedia.org/wiki/Ch%C5%ABdan-no-kamae),
  [Jōdan-no-kamae](https://en.wikipedia.org/wiki/J%C5%8Ddan-no-kamae),
  [Gedan-no-kamae](https://en.wikipedia.org/wiki/Gedan-no-kamae),
  [Hassō-no-kamae](https://en.wikipedia.org/wiki/Hass%C5%8D-no-kamae),
  [Waki-gamae](https://en.wikipedia.org/wiki/Waki-gamae),
  [Japanese sword combat stances (AKBAN wiki)](https://akban.org/wiki/Japanese_sword_combat_stances),
  [Toyama-ryu kihon: kamae](https://toyamaryu.org/kihon_fundamentals_kamae.htm), and the project's own
  `docs/reference/two_handed_sword_stances.png` (Kasumi: hands at the forehead, blade level, pointing at the opponent).
- Footwork: [Kendo Guide: footwork](https://www.kendo-guide.com/footwork.html),
  [kendo.readthedocs: footwork](https://kendo.readthedocs.io/en/latest/basic/footwork.html),
  [Research Journal of Budo 47(2): fumikomi-ashi vs okuri-ashi strikes](https://www.jstage.jst.go.jp/article/budo/47/2/47_85/_article/-char/en),
  [Kendo Jidai: left-foot techniques](https://kendojidai.com/2021/11/15/essential-left-foot-techniques-takenaka-kentaro).
- Body and hands: [kenshi247: tenouchi for men cutting](https://kenshi247.net/blog/2012/01/14/tenouchi-for-men-cutting/),
  [Rakushinkan: article on hasuji](https://sites.google.com/site/rakushinkan/blog/article-on-hasuji),
  [Kendo Jidai: tenouchi and sharpness of strikes](https://kendojidai.com/2022/10/17/tenouchi-and-sharpness-of-strikeskoda-kunihide/),
  [Research Journal of Budo 51(1): hand forces and tip speed](https://www.jstage.jst.go.jp/article/budo/51/1/51_1/_article/-char/en),
  [Kendo Jidai: the left foot is the foundation (Kamei Toru)](https://kendojidai.com/2024/09/23/the-left-foot-is-the-foundation-of-kendo-kamei-toru/),
  [Tameshigiri (Wikipedia)](https://en.wikipedia.com/wiki/Tameshigiri).
- Iai: [budo-notes: ZNKR iai Mae](https://rickardvh.github.io/budo-notes/iaido/znkr/mae.html) (nukitsuke, furikaburi,
  kirioroshi, chiburi, noto), [AUSKF iaido judging points](https://sites.google.com/view/auskf-draft/department/iaido/iaido-judging-points),
  [Iai (Wikipedia)](https://www.wikipedia.com/wiki/Iai).
- Parries: [budo-notes: ZNKR iai Ukenagashi](https://rickardvh.github.io/budo-notes/iaido/znkr/ukenagashi.html),
  [Stenudd: aikibatto ukenagashi](https://www.stenudd.com/aikibatto/shoden05b.htm),
  [kenshi247 on shinogi, suriage and harai](https://kenshi247.net/?p=12403),
  [Nihon Kendo Kata (UW)](https://depts.washington.edu/ombkendo/kata.html).
- Thrust and strike size: [Tsuki (Wikipedia)](https://en.wikipedia.org/wiki/Tsuki),
  [kenshi247: Kobayashi's katate-zuki](https://kenshi247.net/blog/2009/03/24/meijin-no-waza-1-kobayashi-mitsuru-hanshis-katatezuki/),
  [Kendo Guide: training different cut sizes](https://www.kendo-guide.com/training-different-cut-sizes-and-styles.html),
  [Kendo Guide: small men](https://kendo-guide.com/small_men.html).

## Principles, and the Kage moves they change

1. **Kamae are distinct and readable.** Chūdan: hands a fist in front of the navel, point at the opponent's throat.
   Jōdan: hands above the forehead, blade up and back. Gedan: point lowered to the knee. Hassō: hilt by the right shoulder,
   blade nearly upright. Waki: blade hidden low behind the body, only the pommel showing. Kasumi: hands at the
   forehead beside the head, the blade level and pointing at the opponent's eyes, edge up.
   *Changes:* Mirror Guard holds a true **Kasumi at head height** (was at the chest); Crimson Crescent winds up from
   **Hassō**; Iai, Shadow Step and Low Reap load from **Waki**; Kage Counter thrusts out of Kasumi; Kage Cleave keeps Jōdan.
2. **The body drives the cut, the arms deliver it.** Power runs from the rear foot's push through the hips and trunk into
   the hands; the hips move first and the blade arrives last. *Changes:* every cut gets a load key (weight back, hips
   turned away, the blade lagging) and the hips and lead foot travel forward before the arms extend. The arm's last
   push and the blade's arrival land on the impact key; the hit frames do not move.
3. **The left (rear) hand powers and steers, the right (lead) hand guides; both stay on the tsuka.** The rear hand
   finishes on the centre line in front of the belly; pulling with the right hand drags the point off line.
   *Changes:* every two-handed frame of every katana move keeps the near fist on the handle (tested, every frame);
   impact keys finish with the near (rear) fist low on the centre line and the far fist ahead of it. The pommel strike
   and the punches stay one-handed by design (SAN-113).
4. **Hasuji: the edge leads along the cut path.** A blade that travels spine first or twisted bounces instead of
   cutting. A rising cut (kiri-age, gyaku-kesa) and a draw from the hip lead with the edge turned up; a falling cut
   (kiri-oroshi, kesa-giri) with the edge down; the swordsman turns the blade over (te-gaeshi) at the top of the
   wind-up. *Changes:* a new pose flag, `Pose.EdgeUp()`, turns the drawn katana over on its own axis (the edge on the
   spine's side). Iai, Rising Moon, Shadow Step and Low Reap cut edge up; the waki carry is edge up like a sword in
   its scabbard; Kasumi is edge up; the overhead and diagonal cuts are edge down. The active frames of every cut
   are tested: the edge faces the blade's travel.
5. **Tenouchi: a squeeze at impact, then the blade stops.** The grip tightens (little and ring fingers) at the moment of
   impact, so the cut is sharp and then held, not flailed. A kiri-oroshi stops a little below horizontal.
   *Changes:* impacts arrive on a fast ease (OutExpo) and settle by a few degrees on a short hold instead of drifting;
   Kage Cleave no longer swings back up after the cut (it was -48 then -28 degrees).
6. **Footwork: suri-ashi, okuri-ashi, fumikomi.** Feet slide; the lead foot moves first and the rear foot is drawn up
   after (it never passes the front foot); a long strike stamps the lead foot (fumikomi) as the arms reach full
   extension. *Changes:* strike keys plant the lead foot further forward on the impact frame and draw the rear
   foot up after it in the follow-through. Root motion is unchanged.
7. **Wind-ups are compact at speed.** Big swings are a training tool; a fast real cut has a short, sharp furikaburi and
   loses no time; the anticipation reads from the body (hips sinking, weight back) more than from a huge arm swing.
   *Changes:* wind-ups load the hips and pull the hands in rather than flinging the blade far behind; Iai, Shadow Step
   and Kage Counter lose their SAN-113-era wind-ups (blade thrown straight back, hands spread).
8. **Follow-through, then zanshin.** The blade continues past the target along its line (kesa ends low by the knee,
   kiri-oroshi below horizontal, kiri-age above the head), then the swordsman stays connected: the point comes back
   onto the opponent before the body relaxes; iai adds chiburi (a sharp flick) and noto. *Changes:* each cut gets a
   follow-through key on its line and a zanshin key (the point back on the opponent, the body still) before the
   recovery blends to chūdan. Iai and Shadow Step finish with a chiburi-like flick.
9. **Parries deflect, they do not stop.** Uke-nagashi raises the blade overhead slanted so the incoming cut slides off
   the shinogi while the hips turn off the line; suriage rides up the opponent's blade. Both flow straight into a cut.
   *Changes:* the high guard slants the blade back over the head like an uke-nagashi; Mirror Guard (Kasumi) answers
   with the Kage Counter thrust out of the same hands.
10. **Tsuki is linear.** A thrust travels along the blade's own line, driven by the hips and the rear hand, the hands
    turning in at the end; it is not an arc. *Changes:* Kage Counter drops from Kasumi into a level thrust whose tip
    travels along the blade during the active frames (tested).
11. **Carrying the sword on the move.** A swordsman running with a drawn blade keeps both hands on it, held low and
    forward (a low chūdan) or at the hip, never trailing in one hand. *Changes:* the run holds the katana two-handed in
    a low chūdan carry, as the JS armed run keeps the stance arms.
