The Flasm 'language' is a superset of assembely wich gets assembled by the fasm assembeler
so if you need any assembely help you should look at the way its done in fasm

all of the features of the 'language' are optional to use, but once you use some of them you kind of have to use msot of them
but thats kind of its intended use anyways

please note that im not an expert in assembely (im barely even a beginner)

this language was made to simplify some of the assembely quirks

ill go over the features now
1. segments
2. includes
3. entry
4. varible
5. format

# segments
in assembely PE format you usually have sections or segments where you can specify a few things
in flasm you can use pre-defined segments using `#segment .data`
the predefined segments are

`#segment .text` this is where all of your executable code is stored
`#segment .data` this is where your mutable data is stored
`#segment .bss` this is where your buffer data is stored
`#segment .cdata` this is where your constant data is stored
`#segment .idata` this is where your includes/imports are stored

if the segments are needed, they will automatically be added
usaully you dont have to define them, unless youll manually be adding stuff to them
using `const byte print_message = "Hello World";`
will allocate this null termminated string and the pointer to the first character will be the label 'print_message' (im just trying to use fancy words to say that this string will be inside the exe file which you can use using the label)
and it will put this under #segment .cdata for constant data
using `mutable byte console_input = "";`
will make one you can mutate

but if it doest need any initial value its better to make it int a buffer
`buffer byte console_input = 256;` will reserve some space for your 256 bytes, this is more efficient, because it wont be stored in the executable file because it is uninitialized itll all be filled with zeros and you're more explicit about the size making it easier to notice buffer overflows, which will probally happen lol

also in assembly you use character arrays to store strings, and they're null terminated
so it would be 'hello world',0
or 'h','e','l','l','o',0 would probally also work
but if you are using varibles i have added double quotes wich will automatically add the zero at the end so you can just do "hello" and it would be translated to 'hello',0

if you want to include a method from a dll
like printf for example you can use
```
#include <kernel32.dll>
{
    [ExitProcess]
}
```

you specify what dll you want to use, and then all the methods you want
if there were multiple it would look liek this (i heavent even tested this yet)

```
#include <msvcrt.dll>
{
    [print],
    [printf],
    [fprintf],
    [scanf]
}
```
and per assembly if you wanted to call them, you first put the arguments on the stack and then call it

we can use `entry [entry_name](){}` to setup the entry method and create the label automatically
for example
```
entry main()
{
    push 0
    call [ExitProcess]
}
```
here you also see how you can call external included methods in assembly
so lets say we have this
const byte print_message = '#include ',0
const byte print_format = "%s"

then we can do this to print hello world
```
entry main()
{
    push print_message
    push print_format
    call [printf]

    push 0
    call [ExitProcess]
}
```

because of how the stack works you put the arguments on the stack in reverse order
so `printf("%s", print_message);` in c, you would have to put the message first because itll be popped of the stack last

and format is simply exactly the same as in normal fasm except you prefix it with an hash
`#format PE console`

this is so my preparser knows you ment it to be used like that and itll put it on the top of the file so it would actually compile, wise you might get problems later on

thats pretty much it
heres the full hello world file
```
#format PE console

#include <msvcrt.dll>
{
    [printf]
}
#include <kernel32.dll>
{
    [ExitProcess]
}

const byte print_message = "hello world"
const byte print_format = "%s"

entry main()
{
    push print_message
    push print_format
    call [printf]

    push 0
    call [ExitProcess]
}

```
and since everything is optional you can also use just the imports for example, but i would reccomand to always use the segments so it knows where to put it
```
#include <msvcrt.dll>
{
    [printf]
}
#include <kernel32.dll>
{
    [ExitProcess]
}

format PE console
entry main

#segment .text
main:
    push print_message
    push print_format
    call [printf]

    push 0
    call [ExitProcess]

#segment .cdata
print_message db 'hello world',0
print_format db '%s',0
```

this is what it would generate from the imports by the way
```
section '.idata' import data readable writeable 
	;the import table
	dd 0,0,0,RVA flasm_include_msvcrt_name,RVA flasm_include_msvcrt_table
	dd 0,0,0,RVA flasm_include_kernel32_name,RVA flasm_include_kernel32_table
	dd 0,0,0,0,0


	;table declarations
flasm_include_msvcrt_table:
	printf dd RVA flasm_include_printf
	dd 0  ; End of table
flasm_include_kernel32_table:
	ExitProcess dd RVA flasm_include_ExitProcess
	dd 0  ; End of table


	;include declarations
	flasm_include_msvcrt_name db 'msvcrt.dll',0
	flasm_include_kernel32_name db 'kernel32.dll',0


	;method declarations
	flasm_include_printf dw 0
		db 'printf',0
	flasm_include_ExitProcess dw 0
		db 'ExitProcess',0
```

i hope that is enough documentation, if you have any questions, try to contact me somehow

ive since changed some things so here is an example wich has a 6 byte buffer you can easily overflow

```
#format PE console

#include <msvcrt.dll>
{
    [printf],
    [scanf]
}
#include <kernel32.dll>
{
    [ExitProcess]
}

const byte newline = "\n";
const byte console_format = "%s";
const dword init_value = "ABC";
const byte display_text1 = "The varible contains the following: \n";
const byte display_text2 = "Enter a text please into our 6 byte buffer :)\nplease dont exeed out 6 byte limit pls\n>";
const byte display_text3 = "The varible now contains:\n";

buffer byte console_input = 6;
buffer dword affectedTest = 4;




entry main()
{
    ;load the buffer with inital (they need to be a dword for this to work)
    mov eax, [init_value]
    mov [affectedTest], eax

    push display_text1
    push console_format
    call [printf]

    push affectedTest
    push console_format
    call [printf]

    push newline
    push console_format
    call [printf]

    push display_text2
    push console_format
    call [printf]

    push console_input
    push console_format
    call [scanf]

    push display_text3
    push console_format
    call [printf]

    push affectedTest
    push console_format
    call [printf]

    push 0
    call [ExitProcess]
}
```

which will output something like this
```
The varible contains the following: 
ABC
Enter a text please into our 6 byte buffer :)
please dont exeed out 6 byte limit pls
>hello
The varible now contains:
ABC
```
as out input was within the 6 byte buffer the varible hasnt changed
but if we instead of `hello` we input like `hello-world`
```
The varible contains the following: 
ABC
Enter a text please into our 6 byte buffer :)
please dont exeed out 6 byte limit pls
>hello-world
The varible now contains:
world
```

as you can see it overflowed into the next buffer
the reason i used `-` and not ` ` is because scanf only takes till whitespace and puts the rest in a buffer for other scanf's to take from or something along those lines
anyways thats pretty much it
