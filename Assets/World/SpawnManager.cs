using UnityEngine;

// Puts the enemies in the maze and gives each one its 3D model.
// Region 0 = before the first door, region 1 = between door 1 and 2, region 2 = between door 2 and 3.
// Closed doors block enemies too, so each one stays in its own area until you open the way.
public class SpawnManager : MonoBehaviour
{
    public float minDistanceFromPlayer = 15f;
    public int patrolPointsPerEnemy = 5;

    public void SpawnEnemies(MazeGenerator maze)
    {
        Spawn<Guard>("Guard A", maze, 0);
        Spawn<Guard>("Guard B", maze, 1);
        Spawn<GuardDog>("Guard Dog", maze, 1);
        Spawn<Assassin>("Assassin", maze, 2);
    }

    void Spawn<T>(string enemyName, MazeGenerator maze, int regionLevel) where T : EnemyBase
    {
        var region = maze.GetRegionPositions(regionLevel);
        if (region.Count == 0) return;

        // pick a spot that is not too close to the player start
        var farSpots = region.FindAll(p => Vector3.Distance(p, maze.PlayerStart) > minDistanceFromPlayer);
        if (farSpots.Count == 0) farSpots = region;

        GameObject root = new GameObject(enemyName);
        root.transform.position = farSpots[Random.Range(0, farSpots.Count)];

        // build the body that matches the enemy type
        Renderer[] indicators;
        Light light;
        if (typeof(T) == typeof(Guard)) EnemyModels.BuildGuard(root, out indicators, out light);
        else if (typeof(T) == typeof(GuardDog)) EnemyModels.BuildDog(root, out indicators, out light);
        else EnemyModels.BuildRobot(root, out indicators, out light);

        T enemy = root.AddComponent<T>();
        enemy.indicators = indicators;
        enemy.indicatorLight = light;

        if (typeof(T) == typeof(Assassin) && AudioManager.Instance != null)
            AudioManager.Instance.AttachHum(root);

        for (int i = 0; i < patrolPointsPerEnemy; i++)
            enemy.patrolPoints.Add(region[Random.Range(0, region.Count)]);
    }
}
