// using UnityEngine;

// public class ClientManager : MonoSingleton<ClientManager>
// {
//     const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
//     double accumulatedTime = 0.0;
//     uint clientID = 0;
//     ClientNetwork clientNetwork = new ClientNetwork();
//     InputManager inputManager => GetComponent<InputManager>();

//     void Start()
//     {
//         Initialize();
//     }

//     void Initialize()
//     {
//         while (!clientNetwork.Initialize((uint id) => {clientID = id;})) {}
//         Debug.Log("Client network initialized successfully.");
//     }

//     void Update()
//     {
//     }
// }