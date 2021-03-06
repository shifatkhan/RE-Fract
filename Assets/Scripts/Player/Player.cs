﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof(RaycastController))]
public class Player : MonoBehaviour
{
    /// ///////////////////////////////////////////////////////////////////////////////////////////////////
    /* PLEASE PLACE FIELDS IN ORDER (i.e. put members related to jumping with other used jumping fields) */
    ///////////////////////////////////////////////////////////////////////////////////////////////////////

    /* EDITABLE IN INSPECTOR */
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float moveSmoothing = 0.01f;           // time that takes to move character (read docs on SmoothDamp)
    [SerializeField] private float sprintMultiplier = 1.5f;         // multiply moving speed when sprinting
    [SerializeField] private float jumpForce = 17;
    [SerializeField] private float lowJumpGravityMultiplier = 6f;   // make jump low by increasing gravity on character
    [SerializeField] private float staminaRegen = 20f;

    /* FIELDS PUBLIC FOR OTHER SCRIPTS */
    [HideInInspector] public bool onGround;
    [HideInInspector] public bool disableControls = false;
    [HideInInspector] public bool facingRight = true;

    // COMPONENTS, SCRIPTS, AND OBJECT REFERENCES
    protected RaycastController raycastController;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Rigidbody2D rBody;
    //private DinoSoundPlayer charSfxPlayer;
    // INTERNAL INSTANCE MEMBERS
    public Vector2 bodyVelocity;
    private float gravity;                          // general gravity on body

    public float moveDirection = 0f;             // direction in which character is moving
    private float velocityXSmoothing;               // a reference for SmoothDamp method to use
    private bool sprintHeld = false;

    private bool jumpPressed = false;
    private bool isJumping = false;            
    private float fallGravityMultiplier = 2.5f;     // 2.5 means gravity increased by 2.5x when falling after jumping

    private bool interactPressed = false;

    private bool physicsEnabled = true;


    // TODO re-evaluate fields to use?
    /* STATS */
    public float health = 100;
    public float maxHealth;
    public float stamina = 100;
    public float maxStamina;
    
    private bool invincible = false;
    public bool invincibleEnabled = true;
    private bool hurt;
    private int coins; //Not sure which should keep track of coins for now.
	private float invulnerableTime = 1.5f;

    // Called before Start
    private void Awake()
    {
        raycastController = GetComponent<RaycastController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rBody = GetComponent<Rigidbody2D>();
        maxHealth = health;
        maxStamina = stamina;
        //charSfxPlayer = GetComponent<DinoSoundPlayer>();
    }

    // Use this for initialization
    void Start ()
	{
		// Kinematic formula, solve for acceleration going down
		gravity = -(2 * 2.5f) / Mathf.Pow (0.36f, 2);
        physicsEnabled = true;
    }
	
	// Update is called once per frame
	void Update ()
	{
        if(!hurt)
            GetPlayerInput();

        if (stamina < 100)
            stamina += staminaRegen * Time.deltaTime;

        // update animator
        animator.SetFloat("runningSpeed", Mathf.Abs(moveDirection));
        animator.SetBool("isJumping", isJumping);
        //animator.SetBool("isKicking", interactPressed);

        // update sound
        //if(charSfxPlayer != null)
        //{
        //    charSfxPlayer.playWalkSfx(moveDirection != 0 && onGround);  // 1st arg: isWalking bool to play or stop

        //    if (jumpPressed) charSfxPlayer.playJumpSfx();
        //    if (interactPressed) charSfxPlayer.playKickSfx();
        //}
    }

    // FixedUpdate is called at a fixed interval, all physics code should be in here only
    void FixedUpdate()
    {
        if (!hurt)
        {
            if (jumpPressed) OnJumpDown();
            jumpPressed = false;

            if (interactPressed) OnInteract();

            if (physicsEnabled)
            {
                calcBodyVelocity();
                Move(bodyVelocity * Time.deltaTime);
            }
            
        }
        
        //Checks current state of game obj and makes adjustment to velocity if necessary
        CheckState();
    }

    // Get player's input to determine action states
    private void GetPlayerInput()
    {
        if(!disableControls)
        {
            moveDirection = Input.GetAxisRaw("Horizontal");     // 1 = moving right, -1 = moving left, 0 = idle
            sprintHeld = Input.GetButton("Sprint");
            if (Input.GetButtonDown("Jump"))
            {
                jumpPressed = true;
                
            }
            //if (Input.GetButtonDown("Interact"))
            //    interactPressed = true;
            //animator.SetBool("isCrouching", Input.GetButton("Crouch"));
        }
    }

    // Calculate the velocity of player's game object based their state
    protected void calcBodyVelocity()
    {
        
            // gravity makes game object fall at all times
            bodyVelocity.y += gravity * Time.deltaTime;
            // calculate horizontal movement with smoothdamp
            float targetXPosition = moveDirection * moveSpeed;
            if (sprintHeld) targetXPosition *= sprintMultiplier;
            bodyVelocity.x = Mathf.SmoothDamp(bodyVelocity.x, targetXPosition, ref velocityXSmoothing, moveSmoothing); // Params: current position, target position, current velocity (modified by func), time to reach target (smaller = faster)

            // modify player's falling gravity if jumping
            if (isJumping)
            {
                // do a low jump by raising gravity even when ascending if player performs a low jump
                // note we substract -1 with multiplier because engine already apply 1 multiple of gravity 

                // low jump
                if (bodyVelocity.y > 0 && !Input.GetButton("Jump"))
                    bodyVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpGravityMultiplier - 1) * Time.fixedDeltaTime;
                // high jump
                else if (bodyVelocity.y < 0)
                    bodyVelocity += Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1) * Time.fixedDeltaTime;
            }
    }

    // Moves the player. Raycast checks for walls and floors collision.
    public void Move (Vector2 moveAmount)
	{
        // Updates raycast position as game object moves.
        raycastController.UpdateRayOrigins();
        raycastController.collision.Reset();

        // Flips player sprite and raycast if character turns direction
        //if (spriteRenderer.flipX ? (moveDirection > 0) : (moveDirection < 0))       // 1 = moving right, -1 = moving left, 0 = idle
        //    FlipFacingDirection();
        if(moveDirection < 0)
        {
            transform.localScale = new Vector3(transform.localScale.x > 0 ? transform.localScale.x * -1f : transform.localScale.x,
                transform.localScale.y, transform.localScale.z);
            FlipFacingDirection();
        }
        else if(moveDirection > 0)
        {
            transform.localScale = new Vector3(transform.localScale.x < 0 ? transform.localScale.x * -1f : transform.localScale.x,
                transform.localScale.y, transform.localScale.z);
            FlipFacingDirection();
        }

        // Check collisions - if found, moveAmount velocity will be reduced appropriately
        raycastController.checkCollisions(ref moveAmount);

		// Actually changing the velocity of game object
		transform.Translate(moveAmount);
	}

    protected void OnJumpDown()
    {
        //onGround = raycastController.collision.below
        if (onGround)
        {
            // check if player lets go of jump btn before enabling to jump again
            if (!Input.GetButton("Jump"))
                isJumping = false;
            if (!isJumping)
            {
                bodyVelocity = Vector2.up * jumpForce;
                isJumping = true;
                
            }
        }
    }
    
    private void OnInteract()
    {
        interactPressed = false;
    }

	// Sets the facing direction of player
	private void FlipFacingDirection()
    {
        // flip raycast
        raycastController.collision.collDirection = (int)Mathf.Sign(moveDirection);
        // flip sprite
        //spriteRenderer.flipX = !spriteRenderer.flipX;
        facingRight = transform.localScale.x > 0;
    }

    // This method checks the state of the player game object every frame
    protected void CheckState ()
	{
        onGround = raycastController.collision.below;

        // If grounded, reset falling velocity
        // If hit ceiling, set velocity.y to 0 to prevent accumulating
        if (onGround || raycastController.collision.above)
        {
            bodyVelocity.y = 0f;
            isJumping = false;
        }
		//Apparantly, Color isn't something you can modify like transform.position
		//Reduce transparency by half when hurt.
		Color c = spriteRenderer.color;
		if (invincible && invincibleEnabled) 
			c.a = 0.5f;
		else 
			c.a = 1f;
		
		spriteRenderer.color = c;
	}

    // Push the rigid body of player
    public void pushBody(Vector2 direction, float force)
    {
        // Check if there's a rigidbody (enemies don't have one).
        if(rBody != null)
            rBody.AddForce(direction * force);
    }

    IEnumerator tempAddRigidBodyWeight(float time)
    {
        // Check if there's a rigidbody (enemies don't have one).
        if (rBody != null)
        {
            rBody.mass = 1f;
            yield return new WaitForSeconds(time);
            rBody.mass = 0.0001f;
        }
    }

    /// <summary>
    /// Resets the invincble boolean. Used by OnTriggerEnter2D, to return player to vulnerable state 
    /// after slight moment of invincibility.
    /// </summary>
    private void resetInvincible ()
	{
		invincible = false;
	}

	/// <summary>
	/// reset hurt boolean. Used in update(), to allow player to move again.
	/// </summary>
	private void resetHurt ()
	{
		hurt = false;
	}

    public void setPhysicsEnabled(bool enable)
    {
        physicsEnabled = enable;
    }

    /// <summary>
    /// This trigger will check for collision with traps. Not the level.
    /// If collided with traps, player's health reduces and becomes invulnerable
    /// for a short while.
    /// if item, then pick up
    /// </summary>
    /// <param name="other">Other.</param>
    void OnTriggerEnter2D(Collider2D other)
    {
        /*
		if (!invincible) {
			if (other.tag == "Trap") {
				ReceiveDamage ();
			}
		}*/

        if (other.tag == "Coin")
        {
            coins++;
        }
    }

    public void ReceiveDamage(int damage, float enemyXPos)
    {
        if (!invincible)
        {
            bodyVelocity.y = 0;
            if (enemyXPos < transform.position.x) // attacked from left side
            {
                pushBody(new Vector2(1, 0.5f), 0.02f);
                StartCoroutine(tempAddRigidBodyWeight(0.2f));
            }
            else // attacked from right side
            {
                pushBody(new Vector2(-1, 0.5f), 0.02f);
                StartCoroutine(tempAddRigidBodyWeight(0.2f));
            }
                
            //animator.Play("damaged");
            //Receive damage
            health -= damage;

            //Makes slight pause and prevent player from moving when hit
            hurt = true;
            Invoke("resetHurt", 0.2f);

            if (invincibleEnabled)
            {
                //Become invulnerable for 2 seconds
                invincible = true;
                Invoke("resetInvincible", invulnerableTime);
            }
            
        }
    }

    public void UseStamina(float amount)
    {
        //Receive damage
        stamina -= amount;
        
    }
}
