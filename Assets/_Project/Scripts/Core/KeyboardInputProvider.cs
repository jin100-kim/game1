using UnityEngine;

namespace EJR.Game.Core
{
    public sealed class KeyboardInputProvider : IInputVectorProvider
    {
        public Vector2 ReadMove()
        {
            var x = 0f;
            var y = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

            // Fallback to legacy axes so custom mappings still work.
            if (Mathf.Approximately(x, 0f)) x = Input.GetAxisRaw("Horizontal");
            if (Mathf.Approximately(y, 0f)) y = Input.GetAxisRaw("Vertical");

            var input = new Vector2(x, y);
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }
    }
}
