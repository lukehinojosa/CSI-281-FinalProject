using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("The speed at which the player moves.")]
    public float moveSpeed = 8f;

    private CharacterController controller;

    void Awake()
    {
        // Get the CharacterController component attached to this GameObject.
        // It's a more robust way to handle movement and collisions than manually setting the transform position.
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Get input from the horizontal (A/D, Left/Right arrows) and vertical (W/S, Up/Down arrows) axes.
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Create a direction vector based on the input, mapping the vertical input to the Z axis.
        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput);

        // Normalize the vector to prevent faster diagonal movement.
        if (moveDirection.magnitude > 1)
        {
            moveDirection.Normalize();
        }

        // Apply the movement using the CharacterController's Move method.
        // This handles collisions and is smoothed by Time.deltaTime.
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
    }
}