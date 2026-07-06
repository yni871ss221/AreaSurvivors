using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        public CharacterType characterType;
        public GameConfig config;
        public TileGrid grid;
        public WeaponController weapon;
        public Slider hpBar;
        public GameObject damagePopupPrefab;
        public TokenGainPopup tokenGainPopup;
        public DirectionalSpriteAnimator directionalAnimator;
        public Sprite knightSprite;
        public Sprite archerSprite;
        public Sprite mageSprite;
        public Sprite[] knightDownFrames;
        public Sprite[] knightLeftFrames;
        public Sprite[] knightRightFrames;
        public Sprite[] knightUpFrames;
        public Sprite[] archerDownFrames;
        public Sprite[] archerLeftFrames;
        public Sprite[] archerRightFrames;
        public Sprite[] archerUpFrames;
        public Sprite[] mageDownFrames;
        public Sprite[] mageLeftFrames;
        public Sprite[] mageRightFrames;
        public Sprite[] mageUpFrames;
        public bool IsReviving { get; private set; }

        Rigidbody2D body;
        Health health;
        PlayerStats stats;
        AutoRegeneration autoRegen;
        Collider2D hitCollider;
        GridObjectVisual gridVisual;
        CharacterFootprint footprint;
        Vector2 facing = Vector2.down;
        float moveSpeed;
        int paintRadius;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
            if (stats == null) stats = gameObject.AddComponent<PlayerStats>();
            autoRegen = GetComponent<AutoRegeneration>();
            if (autoRegen == null) autoRegen = gameObject.AddComponent<AutoRegeneration>();
            hitCollider = GetComponent<Collider2D>();
            footprint = GetComponent<CharacterFootprint>();
            gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureCharacter(1f);
            if (GetComponent<CharacterOcclusionReveal>() == null) gameObject.AddComponent<CharacterOcclusionReveal>();
            if (tokenGainPopup == null) tokenGainPopup = GetComponentInChildren<TokenGainPopup>(true);
            health.Died += OnDied;
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, CharacterType type)
        {
            config = gameConfig;
            grid = tileGrid;
            characterType = type;
            stats.Initialize(config);
            transform.localScale = Vector3.one * Mathf.Max(0.1f, config.playerVisualScale);
            if (gridVisual != null) gridVisual.ConfigureCharacter(1f);
            ApplyCharacterSprite(type);
            ApplyCurrentStats(true);
            weapon.Configure(config, this);
        }

        public void ApplyCurrentStats(bool fullHeal)
        {
            var current = Stats;
            int beforeMax = health.maxHp;
            int beforeHp = health.currentHp;
            health.defense = current.defense;
            health.SetMax(current.maxHp, fullHeal);
            if (!fullHeal && current.maxHp > beforeMax)
            {
                health.Heal(current.maxHp - beforeMax);
            }
            else if (!fullHeal)
            {
                health.currentHp = Mathf.Min(beforeHp, health.maxHp);
            }
            moveSpeed = current.moveSpeed;
            paintRadius = current.paintRadius;
            if (autoRegen != null)
            {
                autoRegen.amount = current.autoRegen;
                autoRegen.intervalSeconds = config.autoRegenIntervalSeconds;
                autoRegen.popupPrefab = damagePopupPrefab;
                autoRegen.popupOffset = new Vector3(0f, 0.58f, 0f);
            }
            if (weapon != null) weapon.RefreshFromStats();
        }

        void ApplyCharacterSprite(CharacterType type)
        {
            var sprite = type == CharacterType.Archer ? archerSprite : type == CharacterType.Mage ? mageSprite : knightSprite;
            var visual = GetComponentInChildren<PaperMeshVisual>();
            if (sprite != null && visual != null) visual.sprite = sprite;

            if (directionalAnimator != null)
            {
                if (type == CharacterType.Archer)
                {
                    directionalAnimator.SetFrames(archerDownFrames, archerLeftFrames, archerRightFrames, archerUpFrames);
                }
                else if (type == CharacterType.Mage)
                {
                    directionalAnimator.SetFrames(mageDownFrames, mageLeftFrames, mageRightFrames, mageUpFrames);
                }
                else
                {
                    directionalAnimator.SetFrames(knightDownFrames, knightLeftFrames, knightRightFrames, knightUpFrames);
                }
            }
        }

        void Update()
        {
            if (hpBar != null)
            {
                hpBar.value = health.Normalized;
                hpBar.gameObject.SetActive(health.Normalized < 0.999f);
            }
            if (IsReviving) return;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (input.sqrMagnitude > 0.01f) facing = input;

            float enemyTerritoryMultiplier = config.enemyTerritorySlow +
                ProgressionStore.GetLevel(UpgradeType.MovePenaltyReduction) * config.enemyTerritorySlowReductionPerUpgradeLevel;
            var movementSample = MovementSamplePosition();
            float territory = grid.GetMoveMultiplier(movementSample, TileOwner.Player, Mathf.Clamp01(enemyTerritoryMultiplier));
            body.velocity = input * moveSpeed * territory;
            if (directionalAnimator != null) directionalAnimator.Tick(facing, input.sqrMagnitude > 0.01f);
            grid.Paint(movementSample, TileOwner.Player, paintRadius);
        }

        Vector3 MovementSamplePosition()
        {
            if (footprint != null) return footprint.SamplePosition;
            if (hitCollider == null || !hitCollider.enabled) return transform.position;
            var center = hitCollider.bounds.center;
            return new Vector3(center.x, center.y, transform.position.z);
        }

        public Vector2 Facing => facing;
        public Health Health => health;
        public PlayerStats StatsSource => stats;
        public StatBlock Stats => stats != null ? stats.Current : default;
        public float MoveSpeed => moveSpeed;
        public int PaintRadius => paintRadius;
        public float ReviveSeconds => Stats.reviveSeconds;
        public Sprite PortraitSprite => characterType == CharacterType.Archer ? archerSprite : characterType == CharacterType.Mage ? mageSprite : knightSprite;
        public bool CanPerformWorldActions =>
            isActiveAndEnabled &&
            !IsReviving &&
            health != null &&
            !health.IsDead &&
            hitCollider != null &&
            hitCollider.enabled;

        public void ShowTokenGain(int amount)
        {
            if (tokenGainPopup == null || amount <= 0) return;
            tokenGainPopup.ShowAmount(amount);
        }

        void OnDied(Health _)
        {
            StartCoroutine(ReviveRoutine());
        }

        IEnumerator ReviveRoutine()
        {
            IsReviving = true;
            body.velocity = Vector2.zero;
            if (hitCollider != null) hitCollider.enabled = false;
            if (directionalAnimator != null) directionalAnimator.enabled = false;

            var mainVisual = GetComponentInChildren<PaperMeshVisual>();
            var deathPose = new GameObject("Revive Pose");
            deathPose.transform.SetParent(transform, false);
            deathPose.transform.localPosition = new Vector3(0f, -0.24f, 0f);
            deathPose.transform.localScale = new Vector3(0.82f, 0.52f, 1f);
            deathPose.AddComponent<PaperBillboard>();
            var deathVisual = deathPose.AddComponent<PaperMeshVisual>();
            if (mainVisual != null)
            {
                deathVisual.Configure(mainVisual.sprite, mainVisual.color, mainVisual.order + 1);
                mainVisual.visible = false;
            }

            float revive = ReviveSeconds;
            float elapsed = 0f;
            while (elapsed < revive)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.PingPong(elapsed * 7f, 1f);
                deathVisual.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.28f, 0.95f, pulse));
                deathPose.transform.localScale = new Vector3(0.82f + pulse * 0.04f, 0.52f, 1f);
                yield return null;
            }

            health.FullHeal();
            Destroy(deathPose);
            if (mainVisual != null)
            {
                mainVisual.color = Color.white;
                mainVisual.visible = true;
            }
            if (directionalAnimator != null) directionalAnimator.enabled = true;
            if (hitCollider != null) hitCollider.enabled = true;
            IsReviving = false;
        }
    }
}
