using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.Lexica.Greek.Sources.Perseus
{
	/// <summary>
	/// Normalize greek words.
	/// </summary>
	internal static class Normalizations
	{
		/// <summary>
		/// Normalize greek words, for example of the last character is median sigma, turn it to final.
		/// </summary>
		/// <param name="word">The word to normalize.</param>
		/// <returns>Returns the normalized word.</returns>
		public static string NormalizeGreekWord(this string word)
		{
			if (word == null) throw new ArgumentNullException(nameof(word));

			char lastCharacter = word[word.Length - 1];

			switch (lastCharacter)
			{
				case 'σ': // Turn median sigma to final sigma.
					lastCharacter = 'ς';
					break;
				
				default: // No change, return the string itself.
					return word;
			}

			var stringBuilder = new StringBuilder(word, word.Length);

			stringBuilder[stringBuilder.Length - 1] = lastCharacter;

			return stringBuilder.ToString();
		}
	}
}
