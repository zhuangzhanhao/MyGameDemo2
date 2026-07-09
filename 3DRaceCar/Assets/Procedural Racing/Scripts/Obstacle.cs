using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle : MonoBehaviour {
    
    // 游戏管理器的引用
    GameManager manager;
	
    void Start(){
        // 查找场景中的游戏管理器
        manager = GameObject.FindObjectOfType<GameManager>();
    }
	
    void OnCollisionEnter(Collision other){
        // 检测到碰撞时，如果是玩家与障碍物发生碰撞，结束游戏
        if(other.gameObject.transform.root.CompareTag("Player"))
            manager.GameOver();  // 调用游戏管理器的 GameOver 方法结束游戏
    }
}