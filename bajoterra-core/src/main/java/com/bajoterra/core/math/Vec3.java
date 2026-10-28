package com.bajoterra.core.math;

/**
 * Mutable 3D vector. Fields are public on purpose: the simulation hot path mutates vectors in place so that a
 * tick performs no allocation. Coordinates follow the Unity convention: Y is up, +Z is forward, +X is right.
 */
public final class Vec3 {

    public double x;
    public double y;
    public double z;

    public Vec3() {
        this(0, 0, 0);
    }

    public Vec3(double x, double y, double z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vec3 set(double nx, double ny, double nz) {
        this.x = nx;
        this.y = ny;
        this.z = nz;
        return this;
    }

    public Vec3 set(Vec3 other) {
        return set(other.x, other.y, other.z);
    }

    public Vec3 add(Vec3 other) {
        return set(x + other.x, y + other.y, z + other.z);
    }

    public Vec3 add(double dx, double dy, double dz) {
        return set(x + dx, y + dy, z + dz);
    }

    public Vec3 addScaled(Vec3 other, double scale) {
        return set(x + other.x * scale, y + other.y * scale, z + other.z * scale);
    }

    public Vec3 sub(Vec3 other) {
        return set(x - other.x, y - other.y, z - other.z);
    }

    public Vec3 scale(double factor) {
        return set(x * factor, y * factor, z * factor);
    }

    public double dot(Vec3 other) {
        return x * other.x + y * other.y + z * other.z;
    }

    public double lengthSq() {
        return x * x + y * y + z * z;
    }

    public double length() {
        return Math.sqrt(lengthSq());
    }

    public double horizontalLengthSq() {
        return x * x + z * z;
    }

    public double distanceSq(Vec3 other) {
        double dx = x - other.x;
        double dy = y - other.y;
        double dz = z - other.z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double distance(Vec3 other) {
        return Math.sqrt(distanceSq(other));
    }

    public double horizontalDistanceSq(Vec3 other) {
        double dx = x - other.x;
        double dz = z - other.z;
        return dx * dx + dz * dz;
    }

    /** Normalizes in place; a (near) zero vector is left unchanged. */
    public Vec3 normalize() {
        double len = length();
        if (len < 1e-12) {
            return this;
        }
        return scale(1.0 / len);
    }

    public Vec3 setZero() {
        return set(0, 0, 0);
    }

    @Override
    public String toString() {
        return "(" + x + ", " + y + ", " + z + ")";
    }
}
