/// <summary>Weapon values shared by current combat and historical deployment compatibility.</summary>
public readonly struct WeaponProfile
{
    public int Damage { get; }           // per projectile
    public float FireInterval { get; }   // seconds between trigger pulls before Rapid Fire
    public int MagazineSize { get; }
    public float ReloadSeconds { get; }
    public WeaponProfile(int damage, float fireInterval, int magazineSize, float reloadSeconds)
    {
        Damage = damage; FireInterval = fireInterval; MagazineSize = magazineSize; ReloadSeconds = reloadSeconds;
    }
}
