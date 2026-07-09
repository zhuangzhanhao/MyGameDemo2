using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldGenerator : MonoBehaviour {
	
// 可在检查器中看到的变量
	public Material meshMaterial;  // 网格材质
	public float scale;  // 世界比例
	public Vector2 dimensions;  // 世界的尺寸（x: 横向, y: 纵向）
	public float perlinScale;  // 用于生成噪声的比例
    public float waveHeight;  // 波动高度
    public float offset;  // 噪声偏移量
	public float randomness;  // 随机性
	public float globalSpeed;  // 世界移动的速度
	public int startTransitionLength;  // 开始过渡的长度
	public BasicMovement lampMovement;  // 灯光（或方向光）的运动
	public GameObject[] obstacles;  // 障碍物数组
	public GameObject gate;  // 大门对象
	public int startObstacleChance;  // 开始时生成障碍物的概率
	public int obstacleChanceAcceleration;  // 障碍物概率加速
	public int gateChance;  // 大门生成概率
	public int showItemDistance;  // 显示物品的距离
	public float shadowHeight;  // 阴影高度
	
	// 不可见的变量
	Vector3[] beginPoints;  // 世界各部分的起始点，用于过渡效果
	
	GameObject[] pieces = new GameObject[2];  // 存储两个世界部分
	GameObject currentCylinder;  // 当前生成的世界部分（圆柱体）
	
	void Start(){
		// 创建数组，用于存储每个世界部分的起始顶点（用于正确过渡）
		beginPoints = new Vector3[(int)dimensions.x + 1];
		
		// 先生成两个世界部分
		for(int i = 0; i < 2; i++){
			GenerateWorldPiece(i);
		}
	}
	
	void LateUpdate(){
		// 如果第二个部分已经接近玩家，移除第一个部分并更新世界
		if(pieces[1] && pieces[1].transform.position.z <= 0)
			StartCoroutine(UpdateWorldPieces());
		
		// 更新场景中的所有物品，如障碍物和大门
		UpdateAllItems();
	}
	
	void UpdateAllItems(){
		// 查找所有带有 "Item" 标签的物品
		GameObject[] items = GameObject.FindGameObjectsWithTag("Item");
		
		// 对所有物品进行操作
		for(int i = 0; i < items.Length; i++){
			// 获取物品的所有 MeshRenderer
			foreach(MeshRenderer renderer in items[i].GetComponentsInChildren<MeshRenderer>()){
				// 如果物品距离玩家足够近，则显示该物品
				bool show = items[i].transform.position.z < showItemDistance;
				
				// 如果需要显示物品，更新其阴影投射模式
				// 由于世界是圆柱形的，只有底半部分的物体需要阴影
				if(show)
					renderer.shadowCastingMode = (items[i].transform.position.y < shadowHeight) ? ShadowCastingMode.On : ShadowCastingMode.Off;
				
				// 只有在需要显示物品时才启用其渲染器
				renderer.enabled = show;
			}
		}
	}
	
	void GenerateWorldPiece(int i){
		// 创建一个新的世界部分并将其放入数组中
		pieces[i] = CreateCylinder();
		// 根据索引位置调整世界部分的位置
		pieces[i].transform.Translate(Vector3.forward * (dimensions.y * scale * Mathf.PI) * i);
		
		// 更新此部分，使其具有终点并能够移动等
		UpdateSinglePiece(pieces[i]);
	}
	
	IEnumerator UpdateWorldPieces(){
		// 移除第一个部分（当它不再对玩家可见时）
		Destroy(pieces[0]);
		
		// 将第二部分赋值给第一个部分
		pieces[0] = pieces[1];
		
		// 创建一个新的第二部分
		pieces[1] = CreateCylinder();
		
		// 设置新部分的位置并旋转以与第一个部分对齐
		pieces[1].transform.position = pieces[0].transform.position + Vector3.forward * (dimensions.y * scale * Mathf.PI);
		pieces[1].transform.rotation = pieces[0].transform.rotation;
		
		// 更新新生成的世界部分
		UpdateSinglePiece(pieces[1]);
		
		// 等待一帧
		yield return 0;
	}
	
	void UpdateSinglePiece(GameObject piece){
		// 给新生成的部分添加基本运动脚本，使其朝向玩家移动
		BasicMovement movement = piece.AddComponent<BasicMovement>();
		// 设置其移动速度为 globalSpeed（负数表示朝玩家方向移动）
		movement.movespeed = -globalSpeed;
		
		// 设置旋转速度为灯光（方向光）的旋转速度
		if(lampMovement != null)
			movement.rotateSpeed = lampMovement.rotateSpeed;
		
		// 为此部分创建一个终点
		GameObject endPoint = new GameObject();
		endPoint.transform.position = piece.transform.position + Vector3.forward * (dimensions.y * scale * Mathf.PI);
		endPoint.transform.parent = piece.transform;
		endPoint.name = "End Point";
		
		// 改变 Perlin 噪声的偏移量，以确保每个世界部分与上一个不同
		offset += randomness;
		
		// 改变障碍物生成的概率，随着时间的推移障碍物会更多
		if(startObstacleChance > 5)
			startObstacleChance -= obstacleChanceAcceleration;
	}

	public GameObject CreateCylinder(){
		// 创建世界部分的基础对象并命名
		GameObject newCylinder = new GameObject();
		newCylinder.name = "World piece";
		
		// 设置当前圆柱体为新创建的对象
		currentCylinder = newCylinder;
		
		// 给新部分添加 MeshFilter 和 MeshRenderer 组件
		MeshFilter meshFilter = newCylinder.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = newCylinder.AddComponent<MeshRenderer>();
		
		// 给新部分设置材质
		meshRenderer.material = meshMaterial;
		// 生成网格并赋值给 MeshFilter
		meshFilter.mesh = Generate();	
		
		// 添加与网格匹配的 MeshCollider 组件
		newCylinder.AddComponent<MeshCollider>();
		
		return newCylinder;
	}
	
	// 生成并返回新世界部分的网格
	Mesh Generate(){
		// 创建并命名新网格
		Mesh mesh = new Mesh();
		mesh.name = "MESH";
		
		// 创建数组来存储顶点、UV 坐标和三角形
		Vector3[] vertices = null;
		Vector2[] uvs = null;
		int[] triangles = null;
		
		// 创建网格形状并填充数组
		CreateShape(ref vertices, ref uvs, ref triangles);
		
		// 给网格赋值
		mesh.vertices = vertices;
		mesh.uv = uvs;
		mesh.triangles = triangles;
		
		// 重新计算法线
		mesh.RecalculateNormals();
		
		return mesh;
	}
	
	void CreateShape(ref Vector3[] vertices, ref Vector2[] uvs, ref int[] triangles){
	    // 获取该部分在 x 和 z 轴的大小
	    int xCount = (int)dimensions.x;  // 在 x 轴上分割的顶点数量
	    int zCount = (int)dimensions.y;  // 在 z 轴上分割的顶点数量

	    // 初始化顶点和 UV 数组
	    vertices = new Vector3[(xCount + 1) * (zCount + 1)];
	    uvs = new Vector2[(xCount + 1) * (zCount + 1)];

	    int index = 0;

	    // 获取圆柱体的半径
	    float radius = xCount * scale * 0.5f;  // 圆柱的半径

	    // 双重循环遍历 x 和 z 轴的所有顶点
	    for(int x = 0; x <= xCount; x++){
	        for(int z = 0; z <= zCount; z++){
	            // 获取圆柱体的角度，以正确设置顶点位置
	            float angle = x * Mathf.PI * 2f / xCount;

	            // 使用角度的余弦和正弦值来设置顶点
	            vertices[index] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z * scale * Mathf.PI);

	            // 更新 UV 坐标
	            uvs[index] = new Vector2(x * scale, z * scale);

	            // 使用 Perlin 噪声生成 X 和 Z 值
	            float pX = (vertices[index].x * perlinScale) + offset;
	            float pZ = (vertices[index].z * perlinScale) + offset;

	            // 将顶点移动到中心位置（保持 z 坐标）并使用 Perlin 噪声调整位置
	            Vector3 center = new Vector3(0, 0, vertices[index].z);
	            vertices[index] += (center - vertices[index]).normalized * Mathf.PerlinNoise(pX, pZ) * waveHeight;

	            // 处理世界部分之间的平滑过渡
	            if(z < startTransitionLength && beginPoints[0] != Vector3.zero){
	                // 如果是过渡部分，结合 Perlin 噪声和上一个部分的起始点
	                float perlinPercentage = z * (1f / startTransitionLength);
	                Vector3 beginPoint = new Vector3(beginPoints[x].x, beginPoints[x].y, vertices[index].z);
	                vertices[index] = (perlinPercentage * vertices[index]) + ((1f - perlinPercentage) * beginPoint);
	            }
	            else if(z == zCount){
	                // 更新起始点，以确保下一部分的平滑过渡
	                beginPoints[x] = vertices[index];
	            }

	            // 在随机位置生成物品
	            if(Random.Range(0, startObstacleChance) == 0 && !(gate == null && obstacles.Length == 0))
	                CreateItem(vertices[index], x);

	            // 增加顶点索引
	            index++;
	        }
	    }

	    // 初始化三角形数组
	    triangles = new int[xCount * zCount * 6];  // 每个方格有 2 个三角形，每个三角形由 3 个顶点组成，共 6 个顶点

	    // 创建每个方块的基础（三角形的组成更简单）
	    int[] boxBase = new int[6];  // 每个正方形面由 6 个顶点组成（两个三角形）

	    int current = 0;

	    // 遍历 x 轴上的所有位置
	    for(int x = 0; x < xCount; x++){
	        boxBase = new int[]{ 
	            x * (zCount + 1), 
	            x * (zCount + 1) + 1,
	            (x + 1) * (zCount + 1),
	            x * (zCount + 1) + 1,
	            (x + 1) * (zCount + 1) + 1,
	            (x + 1) * (zCount + 1),
	        };

	        // 遍历 z 轴上的所有位置
	        for(int z = 0; z < zCount; z++){
	            // 增加顶点索引并创建三角形
	            for(int i = 0; i < 6; i++){
	                boxBase[i] = boxBase[i] + 1;
	            }

	            // 使用六个顶点填充三角形
	            for(int j = 0; j < 6; j++){                    
	                triangles[current + j] = boxBase[j] - 1;
	            }

	            // 增加当前索引
	            current += 6;
	        }
	    }
	}

	void CreateItem(Vector3 vert, int x){
	    // 获取圆柱体的中心位置，但使用顶点的 z 坐标
	    Vector3 zCenter = new Vector3(0, 0, vert.z);

	    // 检查生成物品的正确位置
	    if(zCenter - vert == Vector3.zero || x == (int)dimensions.x/4 || x == (int)dimensions.x/4 * 3)
	        return;

	    // 随机生成一个物品（大门或障碍物）
	    GameObject newItem = Instantiate((Random.Range(0, gateChance) == 0) ? gate : obstacles[Random.Range(0, obstacles.Length)]);

	    // 旋转物品使其朝向中心
	    newItem.transform.rotation = Quaternion.LookRotation(zCenter - vert, Vector3.up);
	    // 设置物品位置
	    newItem.transform.position = vert;

	    // 将物品作为当前圆柱体的子物体，确保它与世界一起移动
	    newItem.transform.SetParent(currentCylinder.transform, false);
	}

	public Transform GetWorldPiece(){
	    // 返回第一个世界部分的 Transform
	    return pieces[0].transform;
	}

}
