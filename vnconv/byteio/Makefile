SRCS = byteio.cpp
OBJS = byteio.o
OPTFLAGS="-O3"

all: libbyteio.a

libbyteio.a : ${OBJS}
	${AR} ${ARFLAGS} $@ $?
	ranlib $@

${OBJS}: %.o: %.cpp
	${CXX} ${CXXFLAGS} ${OPTFLAGS} -c $< -o $@

clean:
	rm *.o
#	rm libbyteio.a
depend:
	makedepend -- ${CFLAGS} -- ${SRCS}
# DO NOT DELETE
