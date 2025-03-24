/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * COMBAT EFFECT TRACKING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a bit-packed system to record spell effect choices during combat,
 * ensuring consistent synchronization between server and client for randomly
 * selected or variable effects.
 * 
 * USAGE EXAMPLE:
 * var effectStack = new CombatEffectStack();
 * effectStack.PushRandomEffectChoice(randomEffectIndex);
 * uint choiceStack = effectStack.GetStackAsUint();
 * 
 * NOTE:
 * The system uses a 32-bit unsigned integer where each 4-bit segment
 * represents a choice in the effect selection hierarchy.
 * 
 * Created by: Revive101
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

/*
`m_effectChosen` is a stack of 32 bits starting at the LSB of an unsigned 32-bit integer.
The first push onto the stack is the index of the `Spell`s `m_effectList` array.
In the case that this effect is a `RandomSpellEffect`, another 3 bits are pushed onto the stack
to determine which effect to use from the `RandomSpellEffect` `m_effectList` array.

This is recursive, and the stack can be as deep as the bounds of bits available.
Any bit on the stack not used is set to 1.

                                         These 4 bits index into the `RandomSpellEffect` `m_effectList` array.
                                                        |
                                                        |
                                                        |
Frost Beetle 1/5 damage: 1111111111111111111111111111 0000
Frost Beetle 2/5 damage: 1111111111111111111111111111 0001
Frost Beetle 3/5 damage: 1111111111111111111111111111 0010
Frost Beetle 4/5 damage: 1111111111111111111111111111 0011
Frost Beetle 5/5 damage: 1111111111111111111111111111 0100

A stack of all `1` means every effect was chosen.
*/

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Manages the stack of effect choices for random and variable spell effects.
/// </summary>
/// <remarks>
/// Uses a bit-packed unsigned 32-bit integer to record the selection of effects when a spell 
/// contains random or variable effects. The stack records which specific effect variant was 
/// chosen during spell resolution for proper client synchronization.
/// </remarks>
public class CombatEffectStack {

    private uint _stack;

    public CombatEffectStack() => _stack = 0xFFFFFFFF;

    public void PushRandomEffectChoice(int choiceIndex) =>
        // Shift 4 bits, then insert the choice index
        _stack = (_stack << 4) | (uint) (choiceIndex & 0b1111);

    public uint GetStackAsUint() => _stack;

}
