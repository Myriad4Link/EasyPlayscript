# This is a comment.

# This is a external function call. All statements starts with a @ is considered as an external tool call
# which calls the corresponding C# code. The string inside its paranthesis is the parameter. The content 
# inside the square bracket is a "script block".
@script("load tooltip")[
# "aaa \n bbb" is seen as one sentence. For example, the following structure should be parsed as "您好。这里是……？"
你好。
这里是……？

# "aaa \n\n bbb" is seen as two sentences. For example, the following structure should be parsed as "啊、您好\n请问你是？"
啊、您好！

请问你是？

# External function calls can also be made from inside the script block. For example:
@transistion("fade_out")

# But script blocks cannot be nested in another script block. For example, the following is illegal:
# @script("something inside load tooltip")[...] <-- ILLEGAL FOR NESTED BLOCKS!
]
