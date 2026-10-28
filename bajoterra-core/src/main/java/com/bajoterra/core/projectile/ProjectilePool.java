package com.bajoterra.core.projectile;

/** Fixed-capacity pool of projectiles (Object Pool pattern): acquiring and releasing never allocates. */
public final class ProjectilePool {

    private final Projectile[] items;
    private int activeCount;
    private int nextId = 1;

    public ProjectilePool(int capacity) {
        this.items = new Projectile[capacity];
        for (int i = 0; i < capacity; i++) {
            items[i] = new Projectile();
        }
    }

    /** Returns a free projectile marked active, or null when the pool is exhausted. */
    public Projectile acquire() {
        for (Projectile p : items) {
            if (!p.active) {
                p.active = true;
                p.id = nextId++;
                p.hitMask = 0;
                p.owner = null;
                p.slug = null;
                activeCount++;
                return p;
            }
        }
        return null;
    }

    public void release(Projectile p) {
        if (p.active) {
            p.active = false;
            p.owner = null;
            p.slug = null;
            activeCount--;
        }
    }

    public void releaseAll() {
        for (Projectile p : items) {
            release(p);
        }
    }

    public int capacity() {
        return items.length;
    }

    /** Slot access for iteration; check {@link Projectile#active}. */
    public Projectile at(int index) {
        return items[index];
    }

    public int activeCount() {
        return activeCount;
    }
}
