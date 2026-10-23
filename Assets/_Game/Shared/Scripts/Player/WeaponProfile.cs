/// <summary>Weapon values shared by current combat and historical deployment compatibility.</summary>
public readonly struct WeaponProfile
{
    public int Damage { get; }           // per projectile
    public float FireInterval { get; }   // seconds between trigger pulls before Rapid Fire
    public int MagazineSize { get; }
    public float ReloadSeconds { get; }
    public float BurstInterval { get; }
    public float BurstSpread { get; }
    public WeaponProfile(int damage, float fireInterval, int magazineSize, float reloadSeconds)
        : this(damage, fireInterval, magazineSize, reloadSeconds, .10f, 10f) { }
    public WeaponProfile(int damage, float fireInterval, int magazineSize, float reloadSeconds, float burstInterval, float burstSpread)
    {
        Damage = damage; FireInterval = fireInterval; MagazineSize = magazineSize; ReloadSeconds = reloadSeconds;
        BurstInterval = burstInterval; BurstSpread = burstSpread;
    }
}
