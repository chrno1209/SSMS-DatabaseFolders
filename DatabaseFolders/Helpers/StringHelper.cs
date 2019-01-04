using System.Linq;

namespace DatabaseFolders.Helpers
{
    public static class StringHelper
    {
        public static bool Like(this string s, string match, bool CaseInsensitive = true)
        {
            //Nothing matches a null mask or null input string
            if (match == null || s == null)
                return false;
            //Null strings are treated as empty and get checked against the mask.
            //If checking is case-insensitive we convert to uppercase to facilitate this.
            if (CaseInsensitive)
            {
                s = s.ToUpperInvariant();
                match = match.ToUpperInvariant();
            }
            //Keeps track of our position in the primary string - s.
            int j = 0;
            //Used to keep track of multi-character wildcards.
            bool matchanymulti = false;
            //Used to keep track of multiple possibility character masks.
            string multicharmask = null;
            bool inversemulticharmask = false;
            for (int i = 0; i < match.Length; i++)
            {
                //If this is the last character of the mask and its a % or * we are done
                if (i == match.Length - 1 && (match[i] == '%' || match[i] == '*'))
                    return true;
                //A direct character match allows us to proceed.
                var charcheck = true;
                //Backslash acts as an escape character.  If we encounter it, proceed
                //to the next character.
                if (match[i] == '\\')
                {
                    i++;
                    if (i == match.Length)
                        i--;
                }
                else
                {
                    //If this is a wildcard mask we flag it and proceed with the next character
                    //in the mask.
                    if (match[i] == '%' || match[i] == '*')
                    {
                        matchanymulti = true;
                        continue;
                    }
                    //If this is a single character wildcard advance one character.
                    if (match[i] == '_')
                    {
                        //If there is no character to advance we did not find a match.
                        if (j == s.Length)
                            return false;
                        j++;
                        continue;
                    }
                    if (match[i] == '[')
                    {
                        var endbracketidx = match.IndexOf(']', i);
                        //Get the characters to check for.
                        multicharmask = match.Substring(i + 1, endbracketidx - i - 1);
                        //Check for inversed masks
                        inversemulticharmask = multicharmask.StartsWith("^");
                        //Remove the inversed mask character
                        if (inversemulticharmask)
                            multicharmask = multicharmask.Remove(0, 1);
                        //Unescape \^ to ^
                        multicharmask = multicharmask.Replace("\\^", "^");

                        //Prevent direct character checking of the next mask character
                        //and advance to the next mask character.
                        charcheck = false;
                        i = endbracketidx;
                        //Detect and expand character ranges
                        if (multicharmask.Length == 3 && multicharmask[1] == '-')
                        {
                            var newmask = "";
                            var first = multicharmask[0];
                            var last = multicharmask[2];
                            if (last < first)
                            {
                                first = last;
                                last = multicharmask[0];
                            }
                            var c = first;
                            while (c <= last)
                            {
                                newmask += c;
                                c++;
                            }
                            multicharmask = newmask;
                        }
                        //If the mask is invalid we cannot find a mask for it.
                        if (endbracketidx == -1)
                            return false;
                    }
                }
                //Keep track of match finding for this character of the mask.
                var matched = false;
                while (j < s.Length)
                {
                    //This character matches, move on.
                    if (charcheck && s[j] == match[i])
                    {
                        j++;
                        matched = true;
                        break;
                    }
                    //If we need to check for multiple charaters to do.
                    if (multicharmask != null)
                    {
                        var ismatch = multicharmask.Contains(s[j]);
                        //If this was an inverted mask and we match fail the check for this string.
                        //If this was not an inverted mask check and we did not match fail for this string.
                        if (inversemulticharmask && ismatch ||
                            !inversemulticharmask && !ismatch)
                        {
                            //If we have a wildcard preceding us we ignore this failure
                            //and continue checking.
                            if (matchanymulti)
                            {
                                j++;
                                continue;
                            }
                            return false;
                        }
                        j++;
                        matched = true;
                        //Consumse our mask.
                        multicharmask = null;
                        break;
                    }
                    //We are in an multiple any-character mask, proceed to the next character.
                    if (matchanymulti)
                    {
                        j++;
                        continue;
                    }
                    break;
                }
                //We've found a match - proceed.
                if (matched)
                {
                    matchanymulti = false;
                    continue;
                }

                //If no match our mask fails
                return false;
            }
            //Some characters are left - our mask check fails.
            if (j < s.Length)
                return false;
            //We've processed everything - this is a match.
            return true;
        }
    }
}
