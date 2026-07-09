using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        public GameConfig config;
        public TileGrid grid;
        public Transform target;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public DirectionalSpriteAnimator directionalAnimator;
        public int xpValue = 1;
        public int tokenValue;
        public int attackDamage = 3;
        public EnemyKind enemyKind = EnemyKind.Boar;
        public string displayName = "イノシシ";
        public bool elite;
        public bool boss;

        Rigidbody2D body;
        Health health;
        KnockbackReceiver knockback;
        Collider2D[] colliders;
        BoxCollider2D footCollider;
        GridObjectVisual gridVisual;
        CharacterFootprint footprint;
        PaperMeshVisual visual;
        EnemyBounceAnimation bounceAnimation;
        RuntimeSpriteOutline outline;
        CharacterOcclusionReveal reveal;
        EnemySlowEffect slowEffect;
        EnemyHitFlash hitFlash;
        YSort ySort;
        float contactTimer;
        float footProbeDistance = 0.24f;
        float speedMultiplier = 1f;
        float nextEnemyPaintTime;
        int nextEnemyPaintFrame;
        bool hasLastEnemyPaintCell;
        int nextEnemyAnimationFrame;
        float enemyAnimationDelta;
        bool enemyAnimationScheduleInitialized;
        bool hasLastEnemyAnimationMoving;
        bool lastEnemyAnimationMoving;
        int nextEnemyYSortFrame;
        bool enemyYSortScheduleInitialized;
        bool dying;
        bool actionLocked;
        Vector2 facingDirection = Vector2.down;
        Color desiredOutlineColor = Color.black;
        const int BossSortingOffsetPerCell = 35;
        const float NormalEnemyPaintInterval = 0.2f;
        const float EliteEnemyPaintInterval = 0.12f;
        const float BossEnemyPaintInterval = 0.08f;
        const int NormalEnemyPaintFrameInterval = 6;
        const int EliteEnemyPaintFrameInterval = 4;
        const int BossEnemyPaintFrameInterval = 2;
        const int NormalEnemyAnimationFrameInterval = 6;
        const int EliteEnemyAnimationFrameInterval = 2;
        const int BossEnemyAnimationFrameInterval = 1;
        const int NormalEnemyYSortFrameInterval = 4;
        const int EliteEnemyYSortFrameInterval = 2;
        const int BossEnemyYSortFrameInterval = 1;

        public static bool ProbeDisableContactCheck { get; set; }
        public static bool ProbeDisableMoveMultiplier { get; set; }
        public static bool ProbeDisablePaint { get; set; }
        public static bool ProbeDisableAnimation { get; set; }
        public static bool ProbeDisableYSort { get; set; }

        public static void ResetPerformanceProbeOverrides()
        {
            ProbeDisableContactCheck = false;
            ProbeDisableMoveMultiplier = false;
            ProbeDisablePaint = false;
            ProbeDisableAnimation = false;
            ProbeDisableYSort = false;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            if (knockback == null) knockback = gameObject.AddComponent<KnockbackReceiver>();
            colliders = GetComponents<Collider2D>();
            footCollider = GetComponent<BoxCollider2D>();
            footprint = GetComponent<CharacterFootprint>();
            gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureCharacter(1f);
            visual = GetComponentInChildren<PaperMeshVisual>();
            if (visual != null)
            {
                bounceAnimation = visual.GetComponent<EnemyBounceAnimation>();
                if (bounceAnimation == null) bounceAnimation = visual.gameObject.AddComponent<EnemyBounceAnimation>();
            }
            outline = visual != null ? visual.GetComponent<RuntimeSpriteOutline>() : GetComponentInChildren<RuntimeSpriteOutline>();
            reveal = GetComponent<CharacterOcclusionReveal>();
            slowEffect = GetComponent<EnemySlowEffect>();
            ySort = GetComponent<YSort>();
            if (outline == null) Debug.LogError("Enemy prefab is missing RuntimeSpriteOutline on its visual.");
            if (reveal == null) Debug.LogError("Enemy prefab is missing CharacterOcclusionReveal on its root.");
            if (reveal != null) reveal.silhouetteColor = new Color(1f, 0.52f, 0.28f, 0.56f);
            ApplyOutlineStyle();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, int hp, float speedScale)
        {
            Configure(gameConfig, tileGrid, chaseTarget, gameConfig != null ? gameConfig.GetEnemyDefinition(EnemyKind.Boar) : null, hp, speedScale);
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, EnemyDefinition definition, int hp, float speedScale)
        {
            config = gameConfig;
            grid = tileGrid;
            target = chaseTarget;
            if (definition == null && config != null) definition = config.GetEnemyDefinition(EnemyKind.Boar);
            ApplyDefinition(definition);
            health.SetMax(hp);
            body.drag = 0f;
            speedMultiplier = Mathf.Max(0.05f, speedScale);
        }

        public bool IsActionLocked => actionLocked;
        public Vector2 FacingDirection => facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.down;

        public void SetActionLocked(bool locked, Vector2 direction)
        {
            actionLocked = locked;
            if (direction.sqrMagnitude > 0.001f) facingDirection = direction.normalized;
            if (locked && body != null) body.velocity = Vector2.zero;
        }

        void ApplyDefinition(EnemyDefinition definition)
        {
            if (definition == null)
            {
                xpValue = config != null ? config.xpPerEnemy : 1;
                attackDamage = config != null ? config.enemyDamage : 3;
                if (knockback != null) knockback.weight = 1f;
                transform.localScale = Vector3.one * (config != null ? Mathf.Max(0.1f, config.enemyVisualScale) : 1f);
                ConfigureFootCollider(1f);
                return;
            }

            enemyKind = definition.kind;
            displayName = string.IsNullOrEmpty(definition.displayName) ? definition.kind.ToString() : definition.displayName;
            if (directionalAnimator != null)
            {
                directionalAnimator.SetFramesFromResources(definition.spriteKey);
                directionalAnimator.SetPlaybackSpeedMultiplier(definition.animationSpeedMultiplier);
            }
            xpValue = Mathf.Max(0, definition.xpValue);
            tokenValue = Mathf.Max(0, definition.tokenValue);
            elite = definition.elite;
            boss = definition.boss;
            attackDamage = Mathf.Max(0, Mathf.RoundToInt((config != null ? config.enemyDamage : 3) * Mathf.Max(0f, definition.damageMultiplier)));
            if (knockback != null) knockback.weight = KnockbackWeight(definition);
            float visualScale = config != null ? Mathf.Max(0.1f, config.enemyVisualScale) : 1f;
            float cellScale = Mathf.Max(0.1f, definition.cellSize);
            transform.localScale = Vector3.one * visualScale * cellScale;
            speedMultiplier = Mathf.Max(0.05f, definition.speedMultiplier);
            ConfigureFootCollider(cellScale);
            ConfigureCharacterSorting(definition, cellScale);

            desiredOutlineColor = definition.outlineColor;
            ApplyOutlineStyle();
        }

        float KnockbackWeight(EnemyDefinition definition)
        {
            if (definition == null) return 1f;
            if (definition.boss) return ConfiguredWeight(config != null ? config.bossEnemyKnockbackWeight : 0f, 10f);

            float weight = 1f;
            if (IsAdvancedNormalEnemy(definition.kind))
            {
                weight *= ConfiguredWeight(config != null ? config.advancedNormalEnemyKnockbackWeight : 0f, 2f);
            }
            if (definition.elite)
            {
                weight *= ConfiguredWeight(config != null ? config.eliteEnemyKnockbackWeight : 0f, 2f);
            }
            return Mathf.Max(1f, weight);
        }

        static float ConfiguredWeight(float value, float fallback)
        {
            return value > 0f ? value : fallback;
        }

        static bool IsAdvancedNormalEnemy(EnemyKind kind)
        {
            return kind == EnemyKind.Orc ||
                kind == EnemyKind.EliteOrc ||
                kind == EnemyKind.Ogre ||
                kind == EnemyKind.EliteOgre ||
                kind == EnemyKind.SkeletonKnight ||
                kind == EnemyKind.EliteSkeletonKnight ||
                kind == EnemyKind.Lizardman ||
                kind == EnemyKind.EliteLizardman;
        }

        void ConfigureFootCollider(float cellScale)
        {
            if (gridVisual == null) return;
            gridVisual.ConfigureCharacter(Mathf.Max(1f, cellScale));
            footCollider = GetComponent<BoxCollider2D>();
            if (footprint == null) footprint = GetComponent<CharacterFootprint>();
            if (footprint != null) footprint.SetFootCollider(footCollider);
            if (footCollider == null)
            {
                Debug.LogError("Enemy prefab is missing the foot BoxCollider2D.");
                footProbeDistance = Mathf.Max(0.24f, grid != null ? grid.cellSize * 0.5f : 0.24f);
            }
            else if (footprint != null)
            {
                footProbeDistance = Mathf.Max(0.24f, footprint.ProbeRadiusWorld);
            }
            else
            {
                var size = footCollider.size;
                var scale = transform.lossyScale;
                float worldWidth = Mathf.Abs(size.x * scale.x);
                float worldHeight = Mathf.Abs(size.y * scale.y);
                footProbeDistance = Mathf.Max(worldWidth, worldHeight) * 0.5f;
            }
            colliders = GetComponents<Collider2D>();
            RefreshCharacterSorting();
        }

        void ConfigureCharacterSorting(EnemyDefinition definition, float cellScale)
        {
            if (ySort == null) return;

            // Large boss sprites visually overlap walls before their root fully crosses the wall row.
            // Keep YSort active, but shift the threshold forward so front-side bosses render in front.
            ySort.orderOffset = definition != null && definition.boss
                ? Mathf.RoundToInt(Mathf.Max(1f, cellScale) * BossSortingOffsetPerCell)
                : 0;
            ySort.SetUpdateFrameInterval(YSortFrameInterval(definition));
            ySort.enabled = false;
            enemyYSortScheduleInitialized = false;
            ySort.Apply();
        }

        static int YSortFrameInterval(EnemyDefinition definition)
        {
            if (definition != null && definition.boss) return BossEnemyYSortFrameInterval;
            if (definition != null && definition.elite) return EliteEnemyYSortFrameInterval;
            return NormalEnemyYSortFrameInterval;
        }

        void RefreshCharacterSorting()
        {
            gridVisual?.ApplyCharacterYSortPivot();
            if (ySort == null) ySort = GetComponent<YSort>();
            if (ySort == null) return;
            ySort.Refresh();
            ySort.Apply();
        }

        void LateUpdate()
        {
            TickEnemyYSort();
        }

        void ApplyOutlineStyle()
        {
            if (outline == null && visual != null) outline = visual.GetComponent<RuntimeSpriteOutline>();
            if (outline != null) outline.outlineColor = desiredOutlineColor;
            if (reveal == null) reveal = GetComponent<CharacterOcclusionReveal>();
            if (reveal != null) reveal.outlineColor = boss ? Color.red : elite ? Color.yellow : Color.white;
        }

        void Update()
        {
            if (dying || target == null) return;
            if (actionLocked)
            {
                body.velocity = Vector2.zero;
                PaintEnemyTerritory(MovementSamplePosition());
                return;
            }
            if (knockback != null && knockback.Active)
            {
                if (!ProbeDisableAnimation) TickEnemyAnimation(Vector2.down, true);
                return;
            }
            var direction = ((Vector2)(target.position - transform.position)).normalized;
            if (direction.sqrMagnitude > 0.001f) facingDirection = direction;
            if (!ProbeDisableContactCheck && TryHandleGridObjectContact(direction))
            {
                body.velocity = Vector2.zero;
                if (!ProbeDisableAnimation) TickEnemyAnimation(direction, false);
                return;
            }

            var movementSample = MovementSamplePosition();
            float slow = ProbeDisableMoveMultiplier ? 1f : grid.GetMoveMultiplier(movementSample, TileOwner.Enemy, config.playerTerritorySlow);
            if (slowEffect == null) slowEffect = GetComponent<EnemySlowEffect>();
            float weaponSlow = slowEffect != null ? slowEffect.Multiplier : 1f;
            body.velocity = direction * config.enemyBaseSpeed * slow * speedMultiplier * weaponSlow;
            if (!ProbeDisableAnimation) TickEnemyAnimation(direction, body.velocity.sqrMagnitude > 0.01f);
            PaintEnemyTerritory(movementSample);
        }

        void TickEnemyAnimation(Vector2 direction, bool moving)
        {
            if (directionalAnimator == null) return;
            enemyAnimationDelta += Time.deltaTime;

            if (!enemyAnimationScheduleInitialized)
            {
                enemyAnimationScheduleInitialized = true;
                int initialInterval = EnemyAnimationFrameInterval();
                nextEnemyAnimationFrame = Time.frameCount + Mathf.Abs(GetInstanceID()) % initialInterval;
            }

            bool movementStateChanged = hasLastEnemyAnimationMoving && moving != lastEnemyAnimationMoving;
            if (!movementStateChanged && Time.frameCount < nextEnemyAnimationFrame) return;

            int interval = EnemyAnimationFrameInterval();
            directionalAnimator.Tick(direction, moving, enemyAnimationDelta);
            enemyAnimationDelta = 0f;
            hasLastEnemyAnimationMoving = true;
            lastEnemyAnimationMoving = moving;

            int jitter = Mathf.Abs(GetInstanceID()) % interval;
            nextEnemyAnimationFrame = Time.frameCount + interval + jitter;
        }

        int EnemyAnimationFrameInterval()
        {
            if (boss) return BossEnemyAnimationFrameInterval;
            if (elite) return EliteEnemyAnimationFrameInterval;
            return NormalEnemyAnimationFrameInterval;
        }

        void TickEnemyYSort()
        {
            if (ProbeDisableYSort) return;
            if (ySort == null) return;

            int interval = Mathf.Max(1, ySort.updateFrameInterval);
            if (!enemyYSortScheduleInitialized)
            {
                enemyYSortScheduleInitialized = true;
                nextEnemyYSortFrame = Time.frameCount + (interval > 1 ? Mathf.Abs(GetInstanceID()) % interval : 0);
            }

            if (Time.frameCount < nextEnemyYSortFrame) return;

            ySort.Apply();
            int jitter = interval > 1 ? Mathf.Abs(GetInstanceID()) % interval : 0;
            nextEnemyYSortFrame = Time.frameCount + interval + jitter;
        }

        void PaintEnemyTerritory(Vector3 world)
        {
            if (ProbeDisablePaint) return;
            if (grid == null) return;
            if (hasLastEnemyPaintCell &&
                (Time.time < nextEnemyPaintTime || Time.frameCount < nextEnemyPaintFrame)) return;

            grid.Paint(world, TileOwner.Enemy, 1);
            hasLastEnemyPaintCell = true;

            float jitter = (Mathf.Abs(GetInstanceID()) % 100) / 100f;
            nextEnemyPaintTime = Time.time + EnemyPaintInterval() * Mathf.Lerp(0.85f, 1.15f, jitter);
            nextEnemyPaintFrame = Time.frameCount + EnemyPaintFrameInterval();
        }

        float EnemyPaintInterval()
        {
            if (boss) return BossEnemyPaintInterval;
            if (elite) return EliteEnemyPaintInterval;
            return NormalEnemyPaintInterval;
        }

        int EnemyPaintFrameInterval()
        {
            if (boss) return BossEnemyPaintFrameInterval;
            if (elite) return EliteEnemyPaintFrameInterval;
            return NormalEnemyPaintFrameInterval;
        }

        bool TryHandleGridObjectContact(Vector2 direction)
        {
            if (grid == null || direction.sqrMagnitude < 0.001f) return false;
            float probeDistance = Mathf.Max(footProbeDistance, grid.cellSize * 0.5f);
            var probeOrigin = FootProbeOrigin();
            var probePoint = probeOrigin + (Vector3)(direction.normalized * probeDistance);
            if (!TryGetBlockingObject(probePoint, out var record))
            {
                if (!TryGetBlockingObject(probeOrigin, out record)) return false;
            }

            DamageGridObject(record, probePoint);
            return true;
        }

        Vector3 FootProbeOrigin()
        {
            if (footprint != null) return footprint.SamplePosition;
            if (footCollider == null) return transform.position;
            return footCollider.transform.TransformPoint(footCollider.offset);
        }

        Vector3 MovementSamplePosition()
        {
            if (footprint != null) return footprint.SamplePosition;
            return FootProbeOrigin();
        }

        bool TryGetBlockingObject(Vector3 world, out GridObjectRecord record)
        {
            record = null;
            if (grid == null) return false;
            var cell = grid.WorldToCell(world);
            record = grid.GetObject(cell);
            if (record == null || record.instance == null) return false;
            if ((record.flags & (GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding)) == 0) return false;
            return IsAttackableGridObject(record.instance);
        }

        bool IsAttackableGridObject(GameObject instance)
        {
            if (instance == null) return false;
            var barrier = instance.GetComponentInParent<WoodenBarrier>();
            if (barrier != null) return barrier.IsBuilt;
            var ballista = instance.GetComponentInParent<BallistaTower>();
            if (ballista != null) return ballista.IsBuilt;
            var watchTower = instance.GetComponentInParent<WatchTower>();
            if (watchTower != null) return watchTower.IsBuilt;
            return instance.GetComponentInParent<TowerController>() != null;
        }

        void DamageGridObject(GridObjectRecord record, Vector3 hitPoint)
        {
            if (record == null || record.instance == null) return;
            contactTimer -= Time.deltaTime;
            if (contactTimer > 0f) return;
            var otherHealth = record.instance.GetComponentInParent<Health>();
            if (otherHealth == null) return;
            int dealt = otherHealth.Damage(attackDamage, hitPoint);
            if (dealt > 0) DamagePopup.Show(damagePopupPrefab, hitPoint + Vector3.up * 0.18f, dealt, Color.red);
            contactTimer = 0.75f;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (dying) return;
            contactTimer -= Time.deltaTime;
            if (contactTimer > 0f) return;
            var otherHealth = collision.collider.GetComponentInParent<Health>();
            if (otherHealth == null) return;
            var barrier = collision.collider.GetComponentInParent<WoodenBarrier>();
            var ballista = collision.collider.GetComponentInParent<BallistaTower>();
            var watchTower = collision.collider.GetComponentInParent<WatchTower>();
            if (collision.collider.GetComponentInParent<PlayerController>() == null &&
                collision.collider.GetComponentInParent<TowerController>() == null &&
                (barrier == null || !barrier.IsBuilt) &&
                (ballista == null || !ballista.IsBuilt) &&
                (watchTower == null || !watchTower.IsBuilt)) return;
            Vector3 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(transform.position);
            int dealt = otherHealth.Damage(attackDamage, hitPoint);
            if (dealt > 0) DamagePopup.Show(damagePopupPrefab, hitPoint + Vector3.up * 0.18f, dealt, Color.red);
            contactTimer = 0.75f;
        }

        void OnDamaged(Health damagedHealth, int amount)
        {
            if (amount > 0) AudioManager.PlaySfx(SfxTrack.EnemyHit);
            if (amount > 0)
            {
                if (hitFlash == null) hitFlash = GetComponent<EnemyHitFlash>();
                if (hitFlash == null) hitFlash = gameObject.AddComponent<EnemyHitFlash>();
                hitFlash.Play(visual);
            }
            DamagePopup.Show(damagePopupPrefab, damagedHealth.LastDamagePoint + Vector3.up * 0.18f, amount, Color.white);
        }

        void OnDied(Health _)
        {
            if (dying) return;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            dying = true;
            body.velocity = Vector2.zero;
            foreach (var col in colliders) col.enabled = false;
            if (directionalAnimator != null) directionalAnimator.enabled = false;
            bool firstBossDefeatCutscene = boss && GameManager.Instance != null && GameManager.Instance.IsFirstBossDefeatForCurrentStage(this);
            if (firstBossDefeatCutscene) GameManager.Instance.BeginBossDefeatCutscene(this);
            if (firstBossDefeatCutscene)
            {
                yield return GameManager.Instance.WaitForEndingCutsceneCamera(transform);
            }
            if (boss) AudioManager.PlaySfx(SfxTrack.BossDefeatRumble);

            var startPosition = transform.position;
            var startScale = transform.localScale;
            float direction = transform.position.x < 0f ? -1f : 1f;
            var billboard = visual != null ? visual.GetComponent<PaperBillboard>() : null;
            float elapsed = 0f;
            float duration = boss ? 2.3f : 0.48f;
            while (elapsed < duration)
            {
                elapsed += Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (boss)
                {
                    float shake = Mathf.Sin(elapsed * 42f) * Mathf.Lerp(0.08f, 0.01f, t);
                    transform.position = startPosition + new Vector3(shake, -0.35f * t, 0f);
                    if (billboard != null) billboard.rollDegrees = Mathf.Sin(elapsed * 30f) * Mathf.Lerp(5f, 14f, t);
                }
                else if (billboard != null)
                {
                    billboard.rollDegrees = Mathf.Lerp(0f, 82f * direction, t);
                }

                transform.localScale = new Vector3(startScale.x * Mathf.Lerp(1f, 1.08f, t), startScale.y * Mathf.Lerp(1f, 0.36f, t), startScale.z);
                if (visual != null)
                {
                    var color = visual.color;
                    color.a = boss ? Mathf.Lerp(1f, 0.18f, t) : Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));
                    visual.color = color;
                }
                yield return null;
            }

            DropRewards(firstBossDefeatCutscene);
            GameManager.Instance?.RegisterKill();
            if (boss) GameManager.Instance?.BossDefeated(this);
            Destroy(gameObject);
        }

        void DropRewards(bool deferBossTokenReward)
        {
            if (xpOrbPrefab != null && xpValue > 0)
            {
                var orb = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
                var experience = orb.GetComponent<ExperienceOrb>();
                if (experience != null) experience.value = xpValue;
            }

            if (tokenValue > 0 && !(deferBossTokenReward && boss))
            {
                var token = TokenOrb.Spawn(transform.position + Vector3.right * 0.22f, tokenValue);
                if (boss && token != null)
                {
                    token.attractRange = 999f;
                    token.speed = 10f;
                }
            }
            else if (!boss && !elite && RelicEffects.NormalEnemyTokenDropChance > 0f && Random.value < RelicEffects.NormalEnemyTokenDropChance)
            {
                TokenOrb.Spawn(transform.position + Vector3.right * 0.22f, 1);
            }
        }
    }
}
