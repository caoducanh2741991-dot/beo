"""Linux validation launcher: deny native network syscalls before importing any ASR library.
This is a test harness, not a replacement for production ONNX telemetry opt-out.
"""
import ctypes,errno,platform,sys,runpy

def deny_network():
 if sys.platform!='linux' or platform.machine()!='x86_64':raise RuntimeError('Native network-denial harness requires Linux x86_64')
 class Filter(ctypes.Structure):_fields_=[('code',ctypes.c_ushort),('jt',ctypes.c_ubyte),('jf',ctypes.c_ubyte),('k',ctypes.c_uint)]
 class Program(ctypes.Structure):_fields_=[('length',ctypes.c_ushort),('filter',ctypes.POINTER(Filter))]
 # SECCOMP_RET_ERRNO for socket/connect/sendto/sendmsg. Local file I/O is unaffected.
 instructions=[Filter(0x20,0,0,0)]
 for number in (41,42,44,46):instructions.extend([Filter(0x15,0,1,number),Filter(0x06,0,0,0x00050000|errno.EPERM)])
 instructions.append(Filter(0x06,0,0,0x7fff0000));program_array=(Filter*len(instructions))(*instructions);program=Program(len(instructions),program_array);libc=ctypes.CDLL(None,use_errno=True)
 if libc.prctl(38,1,0,0,0) or libc.prctl(22,2,ctypes.byref(program),0,0):raise OSError(ctypes.get_errno(),'Cannot install native network-denial filter')
 sys.addaudithook(lambda e,a: (_ for _ in ()).throw(PermissionError('network disabled during validation')) if e in ('socket.connect','socket.getaddrinfo') else None)

if __name__=='__main__':
 deny_network()
 if sys.argv[1:]==['--self-test']:
  import socket
  try:socket.socket(socket.AF_INET,socket.SOCK_STREAM)
  except PermissionError:print('PASS native socket creation denied before opening any user media',flush=True)
  else:raise RuntimeError('Native socket filter ineffective')
 else:
  script=sys.argv[1];sys.argv=sys.argv[1:];sys.path.insert(0,__import__('os').path.dirname(__import__('os').path.abspath(script)));runpy.run_path(script,run_name='__main__')
