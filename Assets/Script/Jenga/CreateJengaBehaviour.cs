using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.UIElements;

public struct PhysicsJenga : IComponentData { }

public struct CreateJenga : IComponentData
{
    public Entity jengaEntity;
    public int height;
    public int rowCount;
    public Vector3 startPosition;
    public quaternion startRotation;
    public Vector3 boxSize;
}


public class CreateJengaBehaviour : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    [SerializeField] private GameObject jengapiecePrefab;
    [SerializeField] private int height = 10;
    [SerializeField] private int rowCount = 3;

    public void DeclareReferencedPrefabs(List<GameObject> gameObjects)
    {
        gameObjects.Add(jengapiecePrefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var sourceEntity = conversionSystem.GetPrimaryEntity(jengapiecePrefab);

        if (sourceEntity == null) return;

        var boxSize = Vector3.zero;
        var renderer = jengapiecePrefab.GetComponent<Renderer>();

        if (renderer != null) boxSize = renderer.bounds.size;

        var createJengas = new CreateJenga
        {
            jengaEntity = conversionSystem.GetPrimaryEntity(jengapiecePrefab),
            height = this.height,
            rowCount = this.rowCount,
            startPosition = transform.position,
            startRotation = transform.rotation,
            boxSize = boxSize
        };
        dstManager.AddComponentData<CreateJenga>(entity, createJengas);
    }
}

[UpdateBefore(typeof(BuildPhysicsWorld))]
public class CreateJengaSystem : ComponentSystem
{
    private EntityQuery mainGroup;

    protected override void OnCreate()
    {
        mainGroup = GetEntityQuery(ComponentType.ReadOnly<CreateJenga>());
    }

    protected override void OnUpdate()
    {
        var groupEntities = mainGroup.ToEntityArray(Allocator.TempJob);

        foreach(var entity in groupEntities)
        {
            var creator = EntityManager.GetComponentData<CreateJenga>(entity);

            Vector3 boxSize = creator.boxSize;
            int jengaPieceCount = creator.height * creator.rowCount;

            var positions = new NativeArray<Vector3>(jengaPieceCount, Allocator.Temp);
            var rotations = new NativeArray<Quaternion>(jengaPieceCount, Allocator.Temp);
            int jengaIndex = 0;

            for(int height = 0; height < creator.height; height++)
            {
                Vector3 position = Vector3.zero;

                if (height % 2 == 0) 
                {
                    position = new Vector3(-((boxSize.z * 0.5f * creator.rowCount) - boxSize.z * 0.5f), height * boxSize.y, 0.0f); 
                }
                else                 
                { 
                    position = new Vector3(0.0f, height * boxSize.y, -((boxSize.z * 0.5f * creator.rowCount) - boxSize.z * 0.5f));
                }

                for (int cell = 0; cell < creator.rowCount; cell++)
                {
                    Vector3 shiftPos = Vector3.zero;

                    if (height % 2 == 0) 
                    {
                        rotations[jengaIndex] = Quaternion.Euler(0.0f, 90.0f, 0.0f);
                        shiftPos.x = boxSize.z * cell; 
                    }
                    else                 
                    {
                        rotations[jengaIndex] = Quaternion.Euler(0.0f, 0.0f, 0.0f);
                        shiftPos.z = boxSize.z * cell;
                    }

                    positions[jengaIndex] = position + shiftPos;

                    jengaIndex++;
                }
            }
            var entities = new NativeArray<Entity>(jengaPieceCount, Allocator.Temp);
            EntityManager.Instantiate(creator.jengaEntity, entities);

            var jengaComponent = new PhysicsJenga();
            for (jengaIndex = 0; jengaIndex < entities.Length; jengaIndex++)
            {
                EntityManager.AddComponentData(entities[jengaIndex], jengaComponent);
                
                EntityManager.SetComponentData(entities[jengaIndex], new Rotation() { Value = rotations[jengaIndex] });
                
                EntityManager.SetComponentData(entities[jengaIndex], new Translation() { Value = positions[jengaIndex] });
            }

            entities.Dispose();
            positions.Dispose();
            rotations.Dispose();

            PostUpdateCommands.DestroyEntity(entity);
        }
        groupEntities.Dispose();
    }
}