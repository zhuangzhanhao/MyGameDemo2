using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Car : MonoBehaviour {
	
	// 在 Inspector 窗口中可以看到的变量
	public Rigidbody rb;  // 车辆的刚体，用于物理模拟
	
	public Transform[] wheelMeshes;  // 车辆的轮子网格（模型）
	public WheelCollider[] wheelColliders;  // 车辆的轮子碰撞器（用于物理模拟）
	
	public int rotateSpeed;  // 车辆旋转的速度
	public int rotationAngle;  // 每次旋转的角度
	public int wheelRotateSpeed;  // 轮子的旋转速度
	
	public Transform[] grassEffects;  // 草地特效（在轮子接触草地时显示）
	public Transform[] skidMarkPivots;  // 滑痕的生成位置
	public float grassEffectOffset;  // 草地特效的偏移量
	
	public Transform back;  // 车辆后方的位置，用于施加稳定的向下力
	public float constantBackForce;  // 车辆后方施加的持续向下力
	
	public GameObject skidMark;  // 滑痕预设
	public float skidMarkSize;  // 滑痕的大小
	public float skidMarkDelay;  // 滑痕生成的延迟时间
	public float minRotationDifference;  // 最小旋转差异，用于检测是否有足够的旋转来生成滑痕
	
	public GameObject ragdoll;  // 车辆的 ragdoll 物理对象（当车辆破坏时使用）
	
	// 在 Inspector 窗口不可见的变量
	int targetRotation;  // 目标旋转角度
	WorldGenerator generator;  // 世界生成器，用于获取世界相关数据

	float lastRotation;  // 上一帧的旋转角度
	bool skidMarkRoutine;  // 是否正在生成滑痕的标志
	
	void Start(){
		// 查找世界生成器并启动滑痕生成协程
		generator = GameObject.FindObjectOfType<WorldGenerator>();
		StartCoroutine(SkidMark());
	}
	
	void FixedUpdate(){
		// 更新滑痕和草地特效
		UpdateEffects();
	}
	
	void LateUpdate(){
		// 更新所有轮子的网格位置和旋转
		for(int i = 0; i < wheelMeshes.Length; i++){	
			// 获取轮子碰撞器的世界位置和旋转
			Quaternion quat;
			Vector3 pos;
			
			wheelColliders[i].GetWorldPose(out pos, out quat);
			
			// 设置轮子网格的位置
			wheelMeshes[i].position = pos;
			
			// 旋转轮子，使其看起来像是正在行驶
			wheelMeshes[i].Rotate(Vector3.right * Time.deltaTime * wheelRotateSpeed);
		}
		
		// 如果玩家想要转向，旋转车辆
		if(Input.GetMouseButton(0) || Input.GetAxis("Horizontal") != 0){
			UpdateTargetRotation();
		}
		else if(targetRotation != 0){
			// 否则，旋转回到中心位置
			targetRotation = 0;
		}
		
		// 按照目标角度进行旋转
		Vector3 rotation = new Vector3(transform.localEulerAngles.x, targetRotation, transform.localEulerAngles.z);
		transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(rotation), rotateSpeed * Time.deltaTime);
	}
	
	void UpdateTargetRotation(){
		// 如果是用鼠标旋转
		if(Input.GetAxis("Horizontal") == 0){
			// 获取鼠标的位置（屏幕左右位置）
			if(Input.mousePosition.x > Screen.width * 0.5f){
				// 向右旋转
				targetRotation = rotationAngle;
			}
			else{
				// 向左旋转
				targetRotation = -rotationAngle;
			}
		}
		else{
			// 如果按下了方向键或者 a/d 键，根据水平输入旋转车辆
			targetRotation = (int)(rotationAngle * Input.GetAxis("Horizontal"));
		}
	}
	
	void UpdateEffects(){
		// 如果两个后轮都不接触地面，addForce 为 true
		bool addForce = true;
		// 检查车辆的旋转是否发生了变化
		bool rotated = Mathf.Abs(lastRotation - transform.localEulerAngles.y) > minRotationDifference;
		
		// 处理后轮的草地特效
		for(int i = 0; i < 2; i++){
			// 获取后轮（每次迭代选择一个后轮）
			Transform wheelMesh = wheelMeshes[i + 2];
			
			// 检查当前轮子是否接触地面
			if(Physics.Raycast(wheelMesh.position, Vector3.down, grassEffectOffset * 1.5f)){
				// 如果接触地面，显示草地特效
				if(!grassEffects[i].gameObject.activeSelf)
					grassEffects[i].gameObject.SetActive(true);
				
				// 更新草地特效的高度和滑痕的高度，使其与轮子同步
				float effectHeight = wheelMesh.position.y - grassEffectOffset;
				Vector3 targetPosition = new Vector3(grassEffects[i].position.x, effectHeight, wheelMesh.position.z);
				grassEffects[i].position = targetPosition;
				skidMarkPivots[i].position = targetPosition;
				
				// 如果轮子接触地面，则不需要施加额外的向后力
				addForce = false;
			}
			else if(grassEffects[i].gameObject.activeSelf){
				// 如果轮子没有接触地面，则隐藏草地特效
				grassEffects[i].gameObject.SetActive(false);
			}
		}
		
		// 如果两个后轮都不接触地面，施加向下的稳定力
		if(addForce){
			rb.AddForceAtPosition(back.position, Vector3.down * constantBackForce);
			// 不显示滑痕
			skidMarkRoutine = false;
		}
		else{
			if(targetRotation != 0){
				// 如果车辆正在旋转，显示滑痕
				if(rotated && !skidMarkRoutine){
					skidMarkRoutine = true;
				}
				else if(!rotated && skidMarkRoutine){
					skidMarkRoutine = false;
				}
			}
			else{
				// 如果车辆正在旋转回中心位置，不显示滑痕
				skidMarkRoutine = false;
			}
		}
		
		// 更新最后一次旋转角度
		lastRotation = transform.localEulerAngles.y;
	}
	
	// 车辆摧毁时调用，生成 ragdoll 并禁用车辆对象
	public void FallApart(){
		// 创建 ragdoll 物理对象
		Instantiate(ragdoll, transform.position, transform.rotation);
		// 禁用当前车辆对象
		gameObject.SetActive(false);
	}
	
	// 滑痕生成协程
	IEnumerator SkidMark(){
		// 无限循环生成滑痕
		while(true){
			// 等待滑痕生成的延迟时间
			yield return new WaitForSeconds(skidMarkDelay);
			
			// 如果需要生成滑痕
			if(skidMarkRoutine){
				// 为后轮生成滑痕并将其附加到环境中
				for(int i = 0; i < skidMarkPivots.Length; i++){
					// 实例化滑痕对象
					GameObject newskidMark = Instantiate(skidMark, skidMarkPivots[i].position, skidMarkPivots[i].rotation);
					// 将滑痕附加到世界生成器的一个世界块上
					newskidMark.transform.parent = generator.GetWorldPiece();
					// 设置滑痕的大小
					newskidMark.transform.localScale = new Vector3(1, 1, 4) * skidMarkSize;
				}
			}
		}
	}
}
