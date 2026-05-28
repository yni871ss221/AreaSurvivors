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
        Vector2 facing = Vector2.down;
        float moveSpeed;
        int paintRadius;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            health.Died += OnDied;
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, CharacterType type)
        {
            config = gameConfig;
            grid = tileGrid;
            characterType = type;
            ApplyCharacterSprite(type);
            moveSpeed = config.playerMoveSpeed + ProgressionStore.GetLevel(UpgradeType.MoveSpeed) * 0.18f;
            paintRadius = config.paintRadius + ProgressionStore.GetLevel(UpgradeType.PaintRadius) / 2;
            health.SetMax(config.playerMaxHp + ProgressionStore.GetLevel(UpgradeType.MaxHp) * 5);
            weapon.Configure(config, this);
        }

        void ApplyCharacterSprite(CharacterType type)
        {
            var sprite = type == CharacterType.Archer ? archerSprite : type == CharacterType.Mage ? mageSprite : knightSprite;
            var renderer = GetComponent<SpriteRenderer>();
            if (sprite != null && renderer != null) renderer.sprite = sprite;

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
            if (hpBar != null) hpBar.value = health.Normalized;
            if (IsReviving) return;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (input.sqrMagnitude > 0.01f) facing = input;

            float territory = grid.GetOwner(transform.position) == TileOwner.Enemy ? config.enemyTerritorySlow : 1f;
            body.velocity = input * moveSpeed * territory;
            if (directionalAnimator != null) directionalAnimator.Tick(facing, input.sqrMagnitude > 0.01f);
            grid.Paint(transform.position, TileOwner.Player, paintRadius);
        }

        public Vector2 Facing => facing;
        public Health Health => health;

        void OnDied(Health _)
        {
            StartCoroutine(ReviveRoutine());
        }

        IEnumerator ReviveRoutine()
        {
            IsReviving = true;
            body.velocity = Vector2.zero;
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in renderers) sr.enabled = false;
            float revive = Mathf.Max(1f, config.playerReviveSeconds - ProgressionStore.GetLevel(UpgradeType.ReviveSpeed) * 0.35f);
            yield return new WaitForSeconds(revive);
            health.FullHeal();
            foreach (var sr in renderers) sr.enabled = true;
            IsReviving = false;
        }
    }
}
