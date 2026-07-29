using System.Collections;
using UnityEngine;

/// <summary>
/// Stateless helper methods for launching items. Used by both player and AI managers.
/// </summary>
public static class ItemLaunchers
{
    public static IEnumerator LaunchGreenShellForward(IItemDriver driver, GreenShell shell, Vector3 spawnPosition, Quaternion spawnRotation, float speed = 6000f)
    {
        if (shell == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.15f);

        Vector3 launchDirection = driver.DriverGameObject.transform.forward.normalized;
        shell.LaunchStandalone(spawnPosition, spawnRotation, launchDirection, speed, driver.IsAntiGravity, driver.DriverName);

        yield return new WaitForSeconds(0.25f);
    }

    public static IEnumerator LaunchGreenShellBackward(IItemDriver driver, GreenShell shell, Vector3 spawnPosition, Quaternion spawnRotation, float speed = 3500f)
    {
        if (shell == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.15f);

        Vector3 launchDirection = -driver.DriverGameObject.transform.forward.normalized;
        shell.LaunchStandalone(spawnPosition, spawnRotation, launchDirection, speed, driver.IsAntiGravity, driver.DriverName);

        yield return new WaitForSeconds(0.25f);
    }

    public static IEnumerator LaunchRedShellForward(IItemDriver driver, RedShell shell, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (shell == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.15f);

        shell.EnterProjectile(spawnPosition, spawnRotation, driver.CurrentWaypoint, driver.IsAntiGravity, driver.DriverName);

        yield return new WaitForSeconds(0.25f);
    }

    public static IEnumerator LaunchRedShellBackward(IItemDriver driver, RedShell shell, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (shell == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.15f);

        // Convert to green shell for backward throw
        shell.enabled = false;
        shell.gameObject.SetActive(false);

        GreenShell greenShell = shell.gameObject.GetComponent<GreenShell>();
        if (greenShell == null)
        {
            greenShell = shell.gameObject.AddComponent<GreenShell>();
        }

        greenShell.lifetime = 0;
        greenShell.myVelocity = -driver.DriverGameObject.transform.forward.normalized;
        greenShell.velocityMagOriginal = 3500;
        greenShell.AntiGravity = driver.IsAntiGravity;
        greenShell.who_threw_shell = driver.DriverName;

        shell.gameObject.SetActive(true);

        yield return new WaitForSeconds(0.25f);
    }

    public static IEnumerator LaunchBananaForward(IItemDriver driver, Banana banana, Vector3 spawnPosition, Quaternion spawnRotation, bool fromTriple = false)
    {
        if (banana == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.1f);

        Rigidbody kartRb = driver.GetRigidbody();
        float extraForward = kartRb != null ? driver.DriverGameObject.transform.InverseTransformDirection(kartRb.velocity).z * 200f : 0f;

        banana.DetachFromParent();
        banana.EnterProjectile(spawnPosition, spawnRotation, extraForward, driver.DriverName);
    }

    public static IEnumerator LaunchBananaBackward(IItemDriver driver, Banana banana, Vector3 spawnPosition, Quaternion spawnRotation, bool fromTriple = false)
    {
        if (banana == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.25f);

        banana.DetachFromParent();
        banana.EnterMine(spawnPosition, spawnRotation, driver.DriverName);
    }

    public static IEnumerator LaunchBobombForward(IItemDriver driver, Bobomb bobomb, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (bobomb == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.1f);

        Rigidbody kartRb = driver.GetRigidbody();
        float extraForward = kartRb != null ? driver.DriverGameObject.transform.InverseTransformDirection(kartRb.velocity).z * 400f : 0f;

        bobomb.DetachFromParent();
        bobomb.EnterProjectile(spawnPosition, spawnRotation, extraForward, driver.DriverName);

        AudioSource audio = bobomb.GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.enabled = true;
            audio.Play();
        }
    }

    public static IEnumerator LaunchBobombBackward(IItemDriver driver, Bobomb bobomb, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (bobomb == null || driver == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.1f);

        bobomb.DetachFromParent();
        bobomb.EnterMine(spawnPosition, spawnRotation, driver.DriverName);

        AudioSource audio = bobomb.GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.enabled = true;
            audio.Play();
        }
    }
}

























