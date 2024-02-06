using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TJAudio;

public class PlayerManager : LivingEntity, IDamageable
{
	public static PlayerManager instance;
    public List<ActiveUpgrades> activeUpgrades;
    public PlayerStats playerStats;
    public UpgradeManager upgradeManager;
    public UIManager uIManager;
    public WeaponController weaponController;
    public InputHandler inputHandler;
    public Rigidbody playerRigidbody;
    [SerializeField] public Spawner spawner;
    [SerializeField] private PlayerUI playerUI;
    Camera viewCamera;
    public PlayerMovement playerMovement;
    public CyberwareManager cyberwareManager;
    private int expShardLayer = 8;
    private int expShardAsLayerMask;
    public bool invulnerable, isInteracting, isReloading;
    public Transform muzzle;

    [Header("General")]
    public Material flashMaterial;

    private List<Transform> shards = new List<Transform>();
    public int enemiesSlain;
    private void Awake()
    {
        if(instance==null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
         DontDestroyOnLoad(gameObject);

        inputHandler = GetComponent<InputHandler>();
        playerMovement = GetComponent<PlayerMovement>();
        playerStats = GetComponent<PlayerStats>();
        weaponController = GetComponent<WeaponController>();
        playerRigidbody = GetComponent<Rigidbody>();
        cyberwareManager = GetComponent<CyberwareManager>();

        expShardAsLayerMask = (1 << expShardLayer);

        LivingEntity player = GetComponent<LivingEntity>();
        player.OnDamage += PlayerTakeDamage;
        this.OnDeath += OnPlayerDeath;

        viewCamera = Camera.main;
    }
    protected override void Start()
    {
        startingHealth = playerStats.BaseHealth;
        base.Start();
        WeaponManager.instance.LoadWeaponsUnlocked();
    }
    void Update()
    {
        if(dead)
            return;

        float delta = Time.deltaTime;
        inputHandler.TickInput(delta);
        playerRigidbody.angularVelocity = Vector3.zero;

        if(Time.timeScale==0)
            return;

        invulnerable = playerMovement.animator.GetBool("invulnerable");
        isInteracting = playerMovement.animator.GetBool("isInteracting");
        playerMovement.RechargeRollCharges(delta);
        uIManager.timerText.text = FormatTime(Time.time - uIManager.initialTime);

        if (isInteracting)
            return;

        playerMovement.HandleRolling();
        LookAtCursor();
        PickUpExp();
        PullInExp();
    }
    public void StartGame()
    {
        spawner.NextWave();
    }
    private void LateUpdate(){
        inputHandler.rollFlag = false;
    }
    private void LookAtCursor()
    {
        Vector2 mousePosition = inputHandler.inputActions.PlayerMovement.Camera.ReadValue<Vector2>();
        Ray ray = viewCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100))
        {
            Vector3 lookPoint = hit.point;
            lookPoint.y = transform.position.y;

            transform.LookAt(lookPoint);
        }
    }

    private void PlayerTakeDamage(float damage)
    {
        if(invulnerable)
        {
            Debug.Log($"Player invulnerable, no damage");
            return;
        }
        float mitigatedDamage = damage - (damage*(playerStats.Armor/100f));
        health -= mitigatedDamage;
        // Debug.Log($"Player took {mitigatedDamage} damage");

        if(health<=0&&!dead){
            Die();
        }
    }
    
    private void PickUpExp()
    {
        float range = 0f;
        Vector3 p1 = transform.position;
        float sphereCastRadius = playerStats.PickUpRadius;
        RaycastHit[] hitObjects = Physics.SphereCastAll(p1, sphereCastRadius, transform.forward, range, expShardAsLayerMask);

        foreach(RaycastHit hit in hitObjects)
        {
            // Debug.Log($"Hit {hit.collider.gameObject}");
            hit.collider.gameObject.layer = 9;
            shards.Add(hit.collider.gameObject.transform);
        }
    }
    private void PullInExp()
    {
        for (int i = shards.Count; i > 0; i--)
        {
            shards[i-1].position = Vector3.Lerp(shards[i-1].position, this.transform.position, Time.deltaTime * 5f);
            if(Vector3.Distance(shards[i-1].position, this.transform.position)<1)
            {
                CollectExp(shards[i-1].GetComponent<ExpShardItem>());
                shards.RemoveAt(i-1);
            }
        }
    }
    private void CollectExp(ExpShardItem expShardItem)
    {
        playerStats.GainExp(expShardItem.expShard.expAmount * playerStats.ExpMultiplier);
        Destroy(expShardItem.gameObject);
       // IAudioRequester.instance.PlaySFX("pickUpExp");
    }
    public void AcquireUpgrade(Upgrade upgradeToAdd)
    {
        for (int i = 0; i < activeUpgrades.Count; i++)
        {
            if(activeUpgrades[i].upgrade==upgradeToAdd)
            {
                ActiveUpgrades newUpgrade = new ActiveUpgrades();
                newUpgrade.upgrade = upgradeToAdd;
                newUpgrade.level = activeUpgrades[i].level + 1;
                activeUpgrades[i] = newUpgrade;
                playerStats.ApplyUpgrade(activeUpgrades[i]);

                if (upgradeToAdd.upgradeType == Upgrade.UpgradeType.weapon)
                {
                    weaponController.LoadWeapon(upgradeToAdd.weaponType);
                }

                if (newUpgrade.level == newUpgrade.upgrade.upgradlevels.Length-1)
                {
                    //Remove from upgrades
                    upgradeManager.statUpgrades.Remove(newUpgrade.upgrade);
                }
                return;
            }

        }

        //not added yet
        ActiveUpgrades otherNewUpgrade = new ActiveUpgrades();
        otherNewUpgrade.upgrade = upgradeToAdd;
        otherNewUpgrade.level = 0;
        activeUpgrades.Add(otherNewUpgrade);
        playerStats.ApplyUpgrade(otherNewUpgrade);

        if (upgradeToAdd.upgradeType == Upgrade.UpgradeType.weapon)
            weaponController.LoadWeapon(upgradeToAdd.weaponType);

        else if (upgradeToAdd.upgradeType==Upgrade.UpgradeType.cyberware)
            upgradeManager.LoadCyberware((CyberwareUpgrade)upgradeToAdd);
    }
    public int GetUpgradeLevel(Upgrade upgrade)
    {
        for (int i = 0; i < activeUpgrades.Count; i++)
        {
            if(activeUpgrades[i].upgrade==upgrade)
            {
                return activeUpgrades[i].level + 1;
            }
        }

        return 0;
    }
    public IEnumerator Reload()
    {
        float baseReloadSpeed = weaponController.GetReloadSpeed();
        //IAudioRequester.instance.PlaySFX("reload");
        float secondsRemaining = 0.0f;
        isReloading = true;

        while(secondsRemaining < baseReloadSpeed)
        {
            playerUI.reloadBar.value = secondsRemaining / (baseReloadSpeed * playerStats.ReloadSpeed);
            secondsRemaining += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        weaponController.selectedWeapon.ReloadWeapon();
        playerUI.reloadBar.value = 0;
        isReloading = false;
    }    
    public string FormatTime( float time )
    {
        int minutes = (int) time / 60 ;
        int seconds = (int) time - 60 * minutes;
        int milliseconds = (int) (1000 * (time - minutes * 60 - seconds));
        return string.Format("{0:00}:{1:00}:{2:000}", minutes, seconds, milliseconds );
    }
    private void OnPlayerDeath()
    {
        playerMovement.animator.Play("Death");
        playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        float rewardAmount = RewardsManager.instance.CalculateRewardAmount(Time.time);
        GameManager.instance.saveSystem.RecordRecentGame((int)rewardAmount, Time.time, enemiesSlain, (int)playerStats.expLevel.levelNumber);
        StartCoroutine(uIManager.EndGameUI());
    }
    public void Heal(float amount)
    {
        health += amount;
        if(health > startingHealth * playerStats.BonusHealth/100)
            health = startingHealth * playerStats.BonusHealth/100;
    }
}
